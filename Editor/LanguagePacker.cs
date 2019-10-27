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
        static char[] _disallowedCharsInName = { '{', '}', '\\', '\n', '\t' };

        static Dictionary<string, List<string>> _languages;
        static List<string> _textNames;
        static Dictionary<string, int> _textIndices;


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
                    var languageType = ExcelHelper.GetTrimmedString(i);

                    if (!_languages.TryGetValue(languageType, out var texts))
                    {
                        // 添加新语言时，未曾初始化的文本先填充为 null
                        texts = new List<string>(Math.Max(_textNames.Count * 2, 256));
                        for (int j = 0; j < _textNames.Count; j++)
                        {
                            texts.Add(null);
                        }
                        _languages.Add(languageType, texts);
                    }
                    languageTexts[i - 1] = texts;
                }

                // 读取其他所有行
                while (ExcelHelper.ReadLine())
                {
                    // 读取文本名字
                    var name = ExcelHelper.GetString(0)?.Trim();
                    if (string.IsNullOrEmpty(name)) continue;   // 跳过无名字的行（注释行）

                    if (name.IndexOfAny(_disallowedCharsInName) >= 0)
                        throw ExcelHelper.Exception($"Invalid text name '{name}'", 0);

                    // 添加文本条目
                    if (!_textIndices.TryGetValue(name, out int index))
                    {
                        _textIndices.Add(name, index = _textNames.Count);
                        _textNames.Add(name);
                        foreach (var texts in _languages.Values)
                        {
                            // 添加新文本条目时所有语言都先填充为 null
                            texts.Add(null);
                        }
                    }

                    // 读取文本内容
                    for (int i = 1; i < fieldCount; i++)
                    {
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


        static void ProcessTexts()
        {
            var braceChars = new char[] { '{', '}' };
            var conversionChars = new char[] { '{', '}', '\\' };

            var builder = new StringBuilder(1024);

            string text;
            int index;

            foreach (var lang in _languages)
            {
                var textsList = lang.Value;

                // 第一遍：替换引用
                for (int i = 0; i < textsList.Count; i++)
                {
                    if (string.IsNullOrEmpty(text = textsList[i]))
                    {
                        // 顺便消除 null
                        if (text == null)
                        {
                            textsList[i] = string.Empty;
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
                                        builder.Insert(left, textsList[value] ?? string.Empty);
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

                    textsList[i] = builder.ToString();
                    builder.Clear();
                }

                // 第二遍：处理转义
                for (int i = 0; i < textsList.Count; i++)
                {
                    if (string.IsNullOrEmpty(text = textsList[i])) continue;

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

                    textsList[i] = builder.ToString();
                    builder.Clear();
                }
            }
        }


        static void SortMeta()
        {
            swapIndex = _textNames.Count
            for (int i = 0; i < _textNames.Count; i++)
            {
                if (_textNames[i].StartsWith("@"))
                {

                }
            }
        }


        static void WriteMetaFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    var languages = _languages.ToArray();
                    languages.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

                    

                    // languages
                    writer.Write(languages.Length);
                    for (int i = 0; i < languages.Length; i++)
                    {
                        writer.Write(languages[i].Key);
                    }

                    // text names
                    writer.Write(_textIndices.Count);
                    foreach (var text in _textIndices)
                    {
                        writer.Write(text.Key);
                        writer.Write(text.Value);
                    }
                }
            }
        }


        static void WriteLanguageFile(string path, List<string> texts)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    for (int i = 0; i < texts.Count; i++)
                    {
                        writer.Write(texts[i]);
                    }
                }
            }
        }


        static void ReadDataFromExcels(string sourceFolder)
        {
            try
            {
                _languages = new Dictionary<string, List<string>>();
                _textNames = new List<string>(1024);
                _textIndices = new Dictionary<string, int>(1024);

                var dir = new DirectoryInfo(sourceFolder);
                foreach (var file in dir.EnumerateFiles("*.xlsx", SearchOption.AllDirectories))
                {
                    ReadExcel(file.FullName);
                }

                ProcessTexts();
                SortMeta();
            }
            catch()
            {

            }
        }


        public static bool Build(string sourceFolder, string destinationFolder, out string log)
        {
            var logBuilder = new StringBuilder(1024);
            bool result;
            logBuilder.AppendLine("----------------- START -----------------");
            logBuilder.AppendLine();



            try
            {


                // 写配置文件
                Directory.CreateDirectory(destinationFolder);
                logBuilder.Append("Writing Configuration...");
                WriteConfigurationFile($"{destinationFolder}/Configuration");
                logBuilder.AppendLine("Finished");

                // 写语言包
                foreach (var lang in _languages)
                {
                    logBuilder.Append($"Writing Language {lang.Key}...");
                    WriteLanguageFile($"{destinationFolder}/{lang.Key}", lang.Value.texts);
                    logBuilder.AppendLine("Finished");
                }

                logBuilder.AppendLine();
                logBuilder.AppendLine("---------------- SUCCESS ----------------");
                result = true;
            }
            catch (Exception e)
            {
                logBuilder.AppendLine("Unfinished");

                logBuilder.AppendLine();
                logBuilder.AppendLine("Exception details:");
                logBuilder.AppendLine(e.ToString());

                logBuilder.AppendLine();
                logBuilder.AppendLine("---------------- FAILURE ----------------");
                result = false;
            }

            // 释放缓存区
            fonts = null;
            _languages = null;
            styleNames = null;
            _textIndices = null;

            log = logBuilder.ToString();
            return result;
        }


        [UnityEditor.MenuItem("Assets/Unity Extensions/Build Language Packs")]
        static void BuildLanguagePacks()
        {
            if (Build("Localization", "Assets/StreamingAssets/Localization", out var log))
            {
                UnityEngine.Debug.Log(log);
            }
            else
            {
                UnityEngine.Debug.LogError(log);
            }
        }

    } // class LanguagePacksBuilder

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR