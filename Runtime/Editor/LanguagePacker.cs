#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.Localization.Editor
{
    public static class LanguagePacker
    {
        public const string sourceFolder = "Localization";
        public const string targetFolder = "Assets/StreamingAssets/Localization";
        public const string metaFileName = "meta";

        const string textNameColumnName = "TextName";
        const string textAttributeColumnName = "TextAttribute";
        const string @LanguageName = "@LanguageName";
        const char commentChar = '#';
        const char attributeChar = '@';
        const string autoNumberingMark = "^";
        static char[] disallowedCharsInName = { '{', '}', '\\', '\n', '\t' };


        static Dictionary<string, List<string>> _languageTexts;
        static List<string> _languageTypes;
        static Dictionary<string, int> _textIndices;
        static List<(string name, string attribute)> _textNamesAndAttributes;
        static int _languageAttributeCount;


        internal static
            (
                Dictionary<string, List<string>> languageTexts,
                List<string> languageTypes,
                List<(string name, string attribute)> textNamesAndAttributes,
                int attributeCount
            )
        data => (_languageTexts, _languageTypes, _textNamesAndAttributes, _languageAttributeCount);


        static bool TryGetAutoNumbering(string textName, out string prefix, out int suffix)
        {
            int charIndex = textName.Length - 1;
            for (; charIndex >= 0; charIndex--)
            {
                if (!char.IsDigit(textName[charIndex]))
                {
                    break;
                }
            }

            charIndex++;

            if (charIndex < textName.Length)
            {
                if (int.TryParse(textName.Substring(charIndex), out suffix))
                {
                    prefix = textName.Substring(0, charIndex);
                    return true;
                }
            }

            prefix = null;
            suffix = -1;
            return false;
        }


        static void ReadExcel(string path)
        {
            ExcelHelper.ReadFile(path, sheet =>
            {
                // 读取第一行
                ExcelHelper.ReadLine();
                int fieldCount = ExcelHelper.fieldCount;

                // 暂存当前表格含有的语言的文本列表，用于之后添加条目
                (int column, List<string> textList)[] languages = new (int, List<string>)[fieldCount];
                int languageCount = 0;
                int textNameColumn = -1;
                int textAttributeColumn = -1;

                for (int i = 0; i < fieldCount; i++)
                {
                    var columnName = ExcelHelper.GetString(i).Trim();

                    switch (columnName)
                    {
                        case textNameColumnName:
                            if (textNameColumn >= 0) throw ExcelHelper.Exception($"Column '{textNameColumnName}' repeated.", i);
                            textNameColumn = i;
                            break;

                        case textAttributeColumnName:
                            if (textAttributeColumn >= 0) throw ExcelHelper.Exception($"Column '{textAttributeColumnName}' repeated.", i);
                            textAttributeColumn = i;
                            break;

                        default:
                            if (!string.IsNullOrEmpty(columnName) && columnName[0] != commentChar)  // 排除注释列
                            {
                                if (!_languageTexts.TryGetValue(columnName, out var texts)) // 其他表格可能已经读过这种语言
                                {
                                    // 添加新语言时，未曾初始化的文本先填充为 null
                                    texts = new List<string>(Math.Max(_textNamesAndAttributes.Count * 2, 256));
                                    for (int j = 0; j < _textNamesAndAttributes.Count; j++)
                                    {
                                        texts.Add(null);
                                    }
                                    _languageTexts.Add(columnName, texts);
                                    _languageTypes.Add(columnName);
                                }
                                languages[languageCount++] = (i, texts);
                            }
                            break;
                    }
                }

                if (textNameColumn == -1)
                {
                    Debug.Log($"Sheet '{path}/{sheet}' has no '{textNameColumnName}' column, skipped.");
                    return;
                }

                // 读取其他行

                int autoNumberingIndex = -1;
                string autoNumberingPrefix = null;
                string prevName = null;

                while (ExcelHelper.ReadLine())
                {
                    // 读取文本名字
                    var name = ExcelHelper.GetString(textNameColumn).Trim();
                    if (string.IsNullOrEmpty(name) || name[0] == commentChar) continue;   // 跳过注释行

                    if (name.IndexOfAny(disallowedCharsInName) >= 0)
                        throw ExcelHelper.Exception($"Invalid text name '{name}'", 0);

                    // 处理自动编号
                    if (name == autoNumberingMark)
                    {
                        if (autoNumberingIndex == -1)
                        {
                            if (prevName == null)
                                throw ExcelHelper.Exception($"Automatic numbering can not be first line.", 0);

                            if (!TryGetAutoNumbering(prevName, out autoNumberingPrefix, out autoNumberingIndex))
                                throw ExcelHelper.Exception($"The previous TextName has no valid suffix index.", 0);
                        }

                        name = $"{autoNumberingPrefix}{++autoNumberingIndex}";
                    }
                    else
                    {
                        autoNumberingIndex = -1;
                        prevName = name;
                    }

                    // 读取文本属性
                    string attribute = textAttributeColumn >= 0 ? ExcelHelper.GetString(textAttributeColumn).Trim() : string.Empty;

                    // 添加文本条目
                    if (!_textIndices.TryGetValue(name, out int index))
                    {
                        _textIndices.Add(name, index = _textNamesAndAttributes.Count);
                        _textNamesAndAttributes.Add((name, attribute));
                        foreach (var texts in _languageTexts.Values)
                        {
                            // 添加新文本条目时所有语言都先填充为 null
                            texts.Add(null);
                        }
                    }
                    else if (attribute.Length > 0)
                    {
                        if (_textNamesAndAttributes[index].attribute.Length > 0 && _textNamesAndAttributes[index].attribute != attribute)
                            Debug.LogWarning(ExcelHelper.Warning("Conflicted item detected", textAttributeColumn));

                        // 设置文本属性
                        _textNamesAndAttributes[index] = (name, attribute);
                    }

                    // 读取文本内容
                    for (int i = 0; i < languageCount; i++)
                    {
                        var (column, textList) = languages[i];

                        if (!string.IsNullOrEmpty(textList[index]))
                            Debug.LogWarning(ExcelHelper.Warning("Conflicted item detected", column));

                        textList[index] = ExcelHelper.GetString(column);

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
                            Debug.LogWarning($"Unset item: '{_textNamesAndAttributes[i].name}' in language '{lang.Key}'");
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
            _languageAttributeCount = 0;
            for (int i = _textNamesAndAttributes.Count - 1; i >= _languageAttributeCount; i--)
            {
                var current = _textNamesAndAttributes[i];
                if (current.name[0] == attributeChar)
                {
                    current.name = current.name.Substring(1);

                    var target = _textNamesAndAttributes[_languageAttributeCount];
                    _textNamesAndAttributes[_languageAttributeCount] = current;

                    if (_languageAttributeCount != i)
                    {
                        _textNamesAndAttributes[i] = target;

                        foreach (var textList in _languageTexts.Values)
                        {
                            var targetText = textList[_languageAttributeCount];
                            textList[_languageAttributeCount] = textList[i];
                            textList[i] = targetText;
                        }
                    }

                    _languageAttributeCount++;
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
                _textNamesAndAttributes = new List<(string, string)>(1024);

                int fileCount = 0;
                if (Directory.Exists(sourceFolder))
                {
                    foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.xlsx", SearchOption.AllDirectories))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.Hidden) != FileAttributes.Hidden)
                        {
                            fileCount++;
                            ReadExcel(file);
                        }
                    }
                }

                if (fileCount == 0)
                {
                    Directory.CreateDirectory(sourceFolder);
                    string filePath = $"{sourceFolder}/Sample.xlsx";
                    File.Copy(Path.GetFullPath("Packages/com.yuyang.unity-extensions.localization/Runtime/Editor/EditorResources/Sample.xlsx"), filePath, false);
                    ReadExcel(filePath);
                }

                Process();

                if (LocalizationSettings.instance.outputLogs) Debug.Log("[Localization] Finish reading excels.");
                return true;
            }
            catch (Exception e)
            {
                _languageTexts = null;
                _languageTypes = null;
                _textIndices = null;
                _textNamesAndAttributes = null;

                if (LocalizationSettings.instance.outputLogs) Debug.LogError("[Localization] Failed to read excels.");
                Debug.LogError(e);
                return false;
            }
        }


        public static bool WritePacks()
        {
            try
            {
                Directory.CreateDirectory(targetFolder);
                using (var stream = new FileStream($"{targetFolder}/{metaFileName}", FileMode.Create, FileAccess.Write))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        // language attributes
                        writer.Write(_languageAttributeCount);
                        for (int i = 0; i < _languageAttributeCount; i++)
                        {
                            writer.Write(_textNamesAndAttributes[i].name);
                        }

                        // text names and attributes
                        writer.Write(_textNamesAndAttributes.Count - _languageAttributeCount);
                        for (int i = _languageAttributeCount; i < _textNamesAndAttributes.Count; i++)
                        {
                            writer.Write(_textNamesAndAttributes[i].name);
                            writer.Write(_textNamesAndAttributes[i].attribute);
                        }

                        // languages
                        writer.Write(_languageTypes.Count);
                        for (int i = 0; i < _languageTypes.Count; i++)
                        {
                            writer.Write(_languageTypes[i]);
                            var textList = _languageTexts[_languageTypes[i]];
                            for (int j = 0; j < _languageAttributeCount; j++)
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
                            writer.Write(textList.Count - _languageAttributeCount);
                            for (int i = _languageAttributeCount; i < textList.Count; i++)
                            {
                                writer.Write(textList[i]);
                            }
                        }
                    }
                }

                if (LocalizationSettings.instance.outputLogs) Debug.Log("[Localization] Finish writing packs.");
                return true;
            }
            catch (Exception e)
            {
                if (LocalizationSettings.instance.outputLogs) Debug.LogError("[Localization] Failed to write packs.");
                Debug.LogError(e);
                return false;
            }
            finally
            {
                _languageTexts = null;
                _languageTypes = null;
                _textIndices = null;
                _textNamesAndAttributes = null;
            }
        }


        public static void ShowSourceFolder()
        {
            Directory.CreateDirectory(sourceFolder);
            Application.OpenURL(sourceFolder);
        }

    } // class LanguagePacker

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR