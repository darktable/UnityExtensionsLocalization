#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityExtensions.Localization.Editor
{
    static class LanguagePacker
    {
        public const string sourceFolder = "Localization";
        public const string targetFolder = "Assets/StreamingAssets/Localization";
        public const string metaFileName = "meta";

        const string @LanguageName = "@LanguageName";
        const char commentChar = '#';
        const char attributeChar = '@';
        static char[] disallowedCharsInName = { '{', '}', '\\', '\n', '\t' };


        static Dictionary<string, List<string>> _languageTexts;
        static List<string> _languageTypes;
        static Dictionary<string, int> _textIndices;
        static List<string> _textNames;
        static int _attributeCount;


        internal static
            (
                Dictionary<string, List<string>> languageTexts,
                List<string> languageTypes,
                List<string> textNames,
                int attributeCount
            )
        data => (_languageTexts, _languageTypes, _textNames, _attributeCount);


        static void ReadExcel(string path)
        {
            ExcelHelper.ReadFile(path, sheet =>
            {
                // 读取第一行
                ExcelHelper.ReadLine();
                int fieldCount = ExcelHelper.fieldCount;

                // 暂存当前表格含有的语言的文本列表，用于之后添加条目
                List<string>[] languageTexts = new List<string>[fieldCount - 1];

                for (int i = 1; i < fieldCount; i++)
                {
                    var languageType = ExcelHelper.GetString(i)?.Trim();

                    if (string.IsNullOrEmpty(languageType) || languageType[0] == commentChar)
                    {
                        languageTexts[i - 1] = null;    // 表示注释列
                    }
                    else
                    {
                        if (!_languageTexts.TryGetValue(languageType, out var texts))
                        {
                            // 添加新语言时，未曾初始化的文本先填充为 null
                            texts = new List<string>(Math.Max(_textNames.Count * 2, 256));
                            for (int j = 0; j < _textNames.Count; j++)
                            {
                                texts.Add(null);
                            }
                            _languageTexts.Add(languageType, texts);
                            _languageTypes.Add(languageType);
                        }
                        languageTexts[i - 1] = texts;
                    }
                }

                // 读取其他所有行
                while (ExcelHelper.ReadLine())
                {
                    // 读取文本名字
                    var name = ExcelHelper.GetString(0)?.Trim();
                    if (string.IsNullOrEmpty(name) || name[0] == commentChar) continue;   // 跳过注释行

                    if (name.IndexOfAny(disallowedCharsInName) >= 0)
                        throw ExcelHelper.Exception($"Invalid text name '{name}'", 0);

                    // 添加文本条目
                    if (!_textIndices.TryGetValue(name, out int index))
                    {
                        _textIndices.Add(name, index = _textNames.Count);
                        _textNames.Add(name);
                        foreach (var texts in _languageTexts.Values)
                        {
                            // 添加新文本条目时所有语言都先填充为 null
                            texts.Add(null);
                        }
                    }

                    // 读取文本内容
                    for (int i = 1; i < fieldCount; i++)
                    {
                        if (languageTexts[i - 1] == null) continue; // 跳过注释列

                        if (languageTexts[i - 1][index] == null)
                        {
                            languageTexts[i - 1][index] = ExcelHelper.GetString(i);
                        }
                        else
                        {
                            Debug.LogWarning(ExcelHelper.Warning("Conflicted item detected", i));
                        }
                    }
                }
            });
        }


        static void Process()
        {
            // Process Texts
            var braceChars = new char[] { '{', '}' };
            var conversionChars = new char[] { '{', '}', '\\' };

            var builder = new StringBuilder(1024);

            string text;
            int index;

            foreach (var lang in _languageTexts)
            {
                var textList = lang.Value;

                // 第一遍：替换引用
                for (int i = 0; i < textList.Count; i++)
                {
                    if (string.IsNullOrEmpty(text = textList[i]))
                    {
                        // 顺便消除 null
                        if (text == null)
                        {
                            textList[i] = string.Empty;
                            Debug.LogWarning($"Unset item: '{_textNames[i]}' in language '{lang.Key}'");
                        }
                        continue;
                    }

                    if ((index = text.IndexOfAny(braceChars)) < 0) continue;

                    int left = -1;

                    for (builder.Append(text); index < builder.Length; index++)
                    {
                        switch (builder[index])
                        {
                            case '{':
                                if (left >= 0 || index == builder.Length - 1)
                                    throw new Exception($"Brace-conversion failed: '{text}' in language '{lang.Key}'");

                                if (builder[index + 1] == '{') index++;
                                else left = index;
                                continue;

                            case '}':
                                if (left >= 0)
                                {
                                    var name = builder.ToString(left + 1, index - left - 1);
                                    if (_textIndices.TryGetValue(name, out int value))
                                    {
                                        builder.Remove(left, index - left + 1);
                                        builder.Insert(left, textList[value] ?? string.Empty);
                                        index = left - 1;
                                        left = -1;
                                    }
                                    else throw new Exception($"Text name '{name ?? "null"}' not found: '{text}' in language '{lang.Key}'");
                                }
                                else
                                {
                                    if (index == builder.Length - 1 || builder[index + 1] != '}')
                                        throw new Exception($"Brace-conversion failed: '{text}' in language '{lang.Key}'");

                                    index++;
                                }
                                continue;
                        }
                    }

                    if (left >= 0) throw new Exception($"Brace-conversion failed: '{text}' in language '{lang.Key}'");

                    textList[i] = builder.ToString();
                    builder.Clear();
                }

                // 第二遍：处理转义
                for (int i = 0; i < textList.Count; i++)
                {
                    if (string.IsNullOrEmpty(text = textList[i])) continue;

                    if ((index = text.IndexOfAny(conversionChars)) < 0) continue;

                    for (builder.Append(text); index < builder.Length; index++)
                    {
                        switch (builder[index])
                        {
                            case '\\':
                                if (index == builder.Length - 1)
                                    throw new Exception($"Backslash-conversion failed: '{text}' in language '{lang.Key}'");

                                switch (builder[index + 1])
                                {
                                    case 'n': builder[index] = '\n'; break;
                                    case 't': builder[index] = '\t'; break;
                                    case '\\': break;
                                    default: throw new Exception($"Backslash-conversion failed: '{text}' in language '{lang.Key}'");
                                }

                                builder.Remove(index + 1, 1);
                                continue;

                            case '{':
                            case '}':
                                // 经过替换引用，可以确定此处转义必定正确
                                builder.Remove(index, 1);
                                continue;
                        }
                    }

                    textList[i] = builder.ToString();
                    builder.Clear();
                }
            }

            // 将语言列表按名称排序
            if (!_textIndices.ContainsKey(@LanguageName))
            {
                throw new Exception($"Can't find '{@LanguageName.Substring(1)}' attribute. This attribute is indispensable.");
            }
            int nameIndex = _textIndices[@LanguageName];
            _languageTypes.Sort((a, b) => string.CompareOrdinal(_languageTexts[a][nameIndex], _languageTexts[b][nameIndex]));
            _textIndices = null;    // 你已经没用了

            // 将语言属性移到开头, 并移除属性标记
            _attributeCount = 0;
            for (int i = _textNames.Count - 1; i >= _attributeCount; i--)
            {
                var current = _textNames[i];
                if (current[0] == attributeChar)
                {
                    current = current.Substring(1);

                    var target = _textNames[_attributeCount];
                    _textNames[_attributeCount] = current;
                    _textNames[i] = target;

                    foreach (var textList in _languageTexts.Values)
                    {
                        target = textList[_attributeCount];
                        textList[_attributeCount] = textList[i];
                        textList[i] = target;
                    }

                    _attributeCount++;
                    i++;
                }
            }
        }


        public static bool ReadExcels()
        {
            try
            {
                _languageTexts = new Dictionary<string, List<string>>();
                _languageTypes = new List<string>();
                _textIndices = new Dictionary<string, int>(1024);
                _textNames = new List<string>(1024);

                foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.xlsx", SearchOption.AllDirectories))
                {
                    ReadExcel(file);
                }

                Process();

                Debug.Log("[Localization] Finish reading excels.");
                return true;
            }
            catch (Exception e)
            {
                _languageTexts = null;
                _languageTypes = null;
                _textIndices = null;
                _textNames = null;

                Debug.LogError("[Localization] Failed to read excels.");
                Debug.LogException(e);
                return false;
            }
        }


        public static bool WritePacks(bool clearData)
        {
            try
            {
                Directory.CreateDirectory(targetFolder);
                using (var stream = new FileStream($"{targetFolder}/{metaFileName}", FileMode.Create, FileAccess.Write))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        // text names
                        writer.Write(_attributeCount);
                        writer.Write(_textNames.Count - _attributeCount);
                        for (int i = 0; i < _textNames.Count; i++)
                        {
                            writer.Write(_textNames[i]);
                        }

                        // languages
                        writer.Write(_languageTypes.Count);
                        for (int i = 0; i < _languageTypes.Count; i++)
                        {
                            writer.Write(_languageTypes[i]);
                            var textList = _languageTexts[_languageTypes[i]];
                            for (int j = 0; j < _attributeCount; j++)
                            {
                                writer.Write(textList[j]);
                            }
                        }
                    }
                }

                foreach (var lang in _languageTexts)
                {
                    using (var stream = new FileStream($"{targetFolder}/{lang.Key}", FileMode.Create, FileAccess.Write))
                    {
                        using (var writer = new BinaryWriter(stream))
                        {
                            // texts
                            var textList = lang.Value;
                            writer.Write(textList.Count - _attributeCount);
                            for (int i = _attributeCount; i < textList.Count; i++)
                            {
                                writer.Write(textList[i]);
                            }
                        }
                    }
                }

                Debug.Log("[Localization] Finish writing packs.");
                return true;
            }
            catch (Exception e)
            {
                clearData = true;

                Debug.LogError("[Localization] Failed to write packs.");
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (clearData)
                {
                    _languageTexts = null;
                    _languageTypes = null;
                    _textIndices = null;
                    _textNames = null;
                }
            }
        }

    } // class LanguagePacker

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR