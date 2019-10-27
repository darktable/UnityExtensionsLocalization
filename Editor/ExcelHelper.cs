#if UNITY_EDITOR

using System;
using System.IO;
using ExcelDataReader;

namespace UnityExtensions.Localization.Editor
{
    struct ExcelHelper
    {
        static string _filePath;
        static IExcelDataReader _reader;
        static int _lineNumber;


        public static void ReadFile(string path, Action<string> readSheet)
        {
            using (var stream = File.Open(_filePath = path, FileMode.Open, FileAccess.Read))
            {
                using (_reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        _lineNumber = 0;
                        readSheet(_reader.Name);

                    } while (_reader.NextResult());
                }
            }
        }


        public static int fieldCount => _reader.FieldCount;


        public static bool ReadLine()
        {
            _lineNumber++;
            return _reader.Read();
        }


        public static float GetFloat(int i)
        {
            var type = _reader.GetFieldType(i);

            if (type == typeof(double))
            {
                return (float)_reader.GetDouble(i);
            }

            if (type == typeof(float))
            {
                return _reader.GetFloat(i);
            }

            if (type == typeof(int))
            {
                return _reader.GetInt32(i);
            }

            if (float.TryParse(_reader.GetValue(i)?.ToString(), out float value))
            {
                return value;
            }

            throw Exception($"Can't parse number", i);
        }


        // never null
        public static string GetString(int i)
        {
            return (_reader.GetValue(i)?.ToString()) ?? string.Empty;
        }


        // never null or empty（might throw exception）
        public static string GetTrimmedString(int i)
        {
            var result = (_reader.GetValue(i)?.ToString())?.Trim();

            if (string.IsNullOrEmpty(result))
                throw Exception($"Can't get valid text", i);

            return result;
        }


        public static Exception Exception(string message, int column)
        {
            return new Exception($"{message}: File: {_filePath}, Sheet: {_reader.Name}, Line: {_lineNumber}, Column: {column+1}");
        }


        public static string Warning(string message, int column)
        {
            return $"{message}: File: {_filePath}, Sheet: {_reader.Name}, Line: {_lineNumber}, Column: {column+1}";
        }

    } // struct ExcelHelper

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR