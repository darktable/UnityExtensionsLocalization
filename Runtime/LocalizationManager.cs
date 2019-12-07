using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.Localization
{
    public enum TaskType
    {
        LoadMeta,
        LoadLanguage
    }


    public enum TaskResult
    {
        Success,
        Cancel,
        Failure,
    }


    /// <summary>
    /// Localization Manager
    /// </summary>
    public partial struct LocalizationManager
    {
        const string targetFolder = "Localization";
        const string metaFileName = "meta";
        const string languageName = "LanguageName";
        const float waitToDisposeThread = 8;

        // meta 文件中的数据
        static (string type, string[] attributes)[] _languages;
        static Dictionary<string, int> _languageIndices;
        static Dictionary<string, int> _attributeIndices;
        static Dictionary<string, int> _textIndices;

        // 语言包中的数据
        static string[] _texts;

        // -2: 未加载；-1：已加载meta；>= 0：已加载语言
        static int _languageIndex = -2;

        // 异步加载任务队列
        static AsyncTaskQueue<LoadTask> _taskQueue = new AsyncTaskQueue<LoadTask>();


        // Load Task
        abstract class LoadTask : IQueuedTask<LoadTask>
        {
            public abstract TaskType type { get; }
            public abstract string detail { get; }
            public abstract bool canceled { get; }
            public abstract bool succeeded { get; }
            public abstract void Cancel();
            public abstract void Commit();
            public abstract void Process();
            public abstract bool BeforeEnqueue(QuickLinkedList<LoadTask> tasks, int current);

            Action<TaskResult> _callback;

            public LoadTask(Action<TaskResult> callback)
            {
                _callback = callback;
            }

            void IQueuedTask<LoadTask>.AfterComplete()
            {
                int last = languageIndex;

                if (!canceled && succeeded) Commit();

                var result = canceled ? TaskResult.Cancel : (succeeded ? TaskResult.Success : TaskResult.Failure);
                asyncTaskCompleted?.Invoke((type, result, detail));

                if ((last != -1 || languageIndex != -1) && !canceled && succeeded)
                {
                    UpdateContents();
                }

                _callback?.Invoke(result);
            }
        }


        // Load Meta Task
        class LoadMetaTask : LoadTask
        {
            bool _forceReload;

            protected (string type, string[] attributes)[] _languages;
            protected Dictionary<string, int> _languageIndices;
            protected Dictionary<string, int> _attributeIndices;
            protected Dictionary<string, int> _textIndices;

            protected volatile bool _canceled;
            protected volatile bool _succeeded;


            public override TaskType type => TaskType.LoadMeta;

            public override string detail => metaFileName;

            public override bool canceled => _canceled;

            public override bool succeeded => _succeeded;

            public LoadMetaTask(Action<TaskResult> callback, bool forceReload) : base(callback)
            {
                _forceReload = forceReload;
            }

            public override void Cancel()
            {
                _canceled = true;
            }

            public override void Commit()
            {
                LocalizationManager._languages = _languages;
                LocalizationManager._languageIndices = _languageIndices;
                LocalizationManager._attributeIndices = _attributeIndices;
                LocalizationManager._textIndices = _textIndices;
                _texts = null;
                _languageIndex = -1;
            }

            public override void Process()
            {
                try
                {
                    if (!_canceled)
                    {
                        var path = $"{Application.streamingAssetsPath}/{targetFolder}/{metaFileName}";
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                int attributeCount = reader.ReadInt32();
                                int textCount = reader.ReadInt32();

                                _attributeIndices = new Dictionary<string, int>(attributeCount);
                                for (int i = 0; i < attributeCount; i++)
                                {
                                    if (_canceled) break;
                                    _attributeIndices.Add(reader.ReadString(), i);
                                }

                                _textIndices = new Dictionary<string, int>(textCount);
                                for (int i = 0; i < textCount; i++)
                                {
                                    if (_canceled) break;
                                    _textIndices.Add(reader.ReadString(), i);
                                }

                                _languages = new (string, string[])[reader.ReadInt32()];
                                _languageIndices = new Dictionary<string, int>(_languages.Length);
                                for (int i = 0; i < _languages.Length; i++)
                                {
                                    if (_canceled) break;
                                    _languages[i].type = reader.ReadString();
                                    _languageIndices.Add(_languages[i].type, i);
                                    _languages[i].attributes = new string[attributeCount];
                                    for (int j = 0; j < attributeCount; j++)
                                    {
                                        if (_canceled) break;
                                        _languages[i].attributes[j] = reader.ReadString();
                                    }
                                }
                            }
                        }
                    }
                    _succeeded = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public override bool BeforeEnqueue(QuickLinkedList<LoadTask> tasks, int current)
            {
                if (!_forceReload)
                {
                    if (_languageIndex > -2)
                    {
                        Cancel();
                        return true;
                    }
                    else
                    {
                        int id = tasks.first;
                        while (id != current)
                        {
                            var task = tasks[id];
                            id = tasks.GetNext(id);

                            if (task.type == TaskType.LoadMeta && !task.canceled && task.succeeded)
                            {
                                Cancel();
                                return true;
                            }
                        }

                        while (id != -1)
                        {
                            var task = tasks[id];
                            id = tasks.GetNext(id);

                            if (task.type == TaskType.LoadMeta && !task.canceled)
                            {
                                Cancel();
                                return true;
                            }
                        }
                    }
                }

                foreach (var task in tasks)
                {
                    task.Cancel();
                }

                return true;
            }
        }


        // Load Language Task
        class LoadLanguageTask : LoadTask
        {
            bool _forceReload;

            protected string _languageType;
            protected string[] _texts;

            protected volatile bool _canceled;
            protected volatile bool _succeeded;

            public override TaskType type => TaskType.LoadLanguage;

            public override string detail => _languageType;

            public override bool canceled => _canceled;

            public override bool succeeded => _succeeded;

            public override void Cancel()
            {
                _canceled = true;
            }

            public LoadLanguageTask(string languageType, Action<TaskResult> callback, bool forceReload) : base(callback)
            {
                _languageType = languageType;
                _forceReload = forceReload;
            }

            public override void Commit()
            {
                LocalizationManager._texts = _texts;
                _languageIndex = _languageIndices[_languageType];
            }

            public override void Process()
            {
                try
                {
                    if (!_canceled)
                    {
                        var path = $"{Application.streamingAssetsPath}/{targetFolder}/{_languageType}";
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                _texts = new string[reader.ReadInt32()];
                                for (int i = 0; i < _texts.Length; i++)
                                {
                                    if (_canceled) break;
                                    _texts[i] = reader.ReadString();
                                }
                            }
                        }
                    }
                    _succeeded = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public override bool BeforeEnqueue(QuickLinkedList<LoadTask> tasks, int current)
            {
                if (!_forceReload)
                {
                    if (_languageIndex >= 0 && _languages[_languageIndex].type == _languageType)
                    {
                        Cancel();
                        return true;
                    }
                    else
                    {
                        int id = tasks.first;
                        while (id != current)
                        {
                            var task = tasks[id];
                            id = tasks.GetNext(id);

                            if (task.detail == _languageType && !task.canceled && task.succeeded)
                            {
                                Cancel();
                                return true;
                            }
                        }

                        while (id != -1)
                        {
                            var task = tasks[id];
                            id = tasks.GetNext(id);

                            if (task.detail == _languageType && !task.canceled)
                            {
                                Cancel();
                                return true;
                            }
                        }
                    }
                }

                foreach (var task in tasks)
                {
                    if (task.type == TaskType.LoadLanguage) task.Cancel();
                }

                return true;
            }
        }


#if UNITY_EDITOR

        class LoadExcelMetaTask : LoadMetaTask
        {
            volatile bool _buildCompleted;

            public LoadExcelMetaTask(bool buildCompleted, Action<TaskResult> callback, bool forceReload) : base(callback, forceReload)
            {
                _buildCompleted = buildCompleted;
            }

            public override void Process()
            {
                if (!_canceled)
                {
                    if (!_buildCompleted) Editor.LanguagePacker.ReadExcels();
                    var (languageTexts, languageTypes, textNames, attributeCount) = Editor.LanguagePacker.data;

                    if (!_canceled && languageTexts != null)
                    {
                        _attributeIndices = new Dictionary<string, int>(attributeCount);
                        for (int i = 0; i < attributeCount; i++)
                        {
                            _attributeIndices.Add(textNames[i], i);
                        }

                        _textIndices = new Dictionary<string, int>(textNames.Count - attributeCount);
                        for (int i = attributeCount; i < textNames.Count; i++)
                        {
                            _textIndices.Add(textNames[i], i - attributeCount);
                        }

                        _languages = new (string, string[])[languageTypes.Count];
                        _languageIndices = new Dictionary<string, int>(_languages.Length);
                        for (int i = 0; i < _languages.Length; i++)
                        {
                            _languages[i].type = languageTypes[i];
                            _languageIndices.Add(_languages[i].type, i);
                            _languages[i].attributes = new string[attributeCount];
                            var textList = languageTexts[_languages[i].type];
                            for (int j = 0; j < attributeCount; j++)
                            {
                                _languages[i].attributes[j] = textList[j];
                            }
                        }

                        _succeeded = true;
                    }
                }
            }
        }


        class LoadExcelLanguageTask : LoadLanguageTask
        {
            public LoadExcelLanguageTask(string languageType, Action<TaskResult> callback, bool forceReload) : base(languageType, callback, forceReload)
            {
            }

            public override void Process()
            {
                if (!_canceled)
                {
                    var (languageTexts, _, textNames, attributeCount) = Editor.LanguagePacker.data;

                    if (languageTexts != null)
                    {
                        _texts = new string[textNames.Count - attributeCount];
                        var textList = languageTexts[_languageType];

                        for (int i = 0; i < _texts.Length; i++)
                        {
                            _texts[i] = textList[i + attributeCount];
                        }

                        _succeeded = true;
                    }
                }
            }
        }


        internal static void LoadExcelMetaAsync(bool buildCompleted = false, Action<TaskResult> callback = null, bool forceReload = false)
        {
            var task = new LoadExcelMetaTask(buildCompleted, callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        internal static void LoadExcelLanguageAsync(string languageType, Action<TaskResult> callback = null, bool forceReload = false)
        {
            var task = new LoadExcelLanguageTask(languageType, callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        internal static void LoadExcelLanguageAsync(int languageIndex, Action<TaskResult> callback = null, bool forceReload = false)
        {
            var languageType = _languages[languageIndex].type;
            var task = new LoadExcelLanguageTask(languageType, callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        internal static string GetAllUsedCharacters()
        {
            HashSet<char> chars = new HashSet<char>();
            foreach (var text in _texts)
            {
                foreach (var c in text)
                {
                    chars.Add(c);
                }
            }

            foreach (var c in _languages[_languageIndex].attributes[_attributeIndices[languageName]])
            {
                chars.Add(c);
            }

            var builder = new System.Text.StringBuilder(chars.Count);
            foreach (var c in chars)
            {
                builder.Append(c);
            }

            return builder.ToString();
        }

#endif


        /// <summary>
        /// Trigger on any async task completed.
        /// Every xxxAsync call corresponds a asyncTaskCompleted callback.
        /// </summary>
        public static event Action<(TaskType type, TaskResult result, string detail)> asyncTaskCompleted;


        /// <summary>
        /// Current language type (default Empty before any language is loaded)，you can save this in user data
        /// </summary>
        public static string languageType => _languageIndex < 0 ? string.Empty : _languages[_languageIndex].type;


        /// <summary>
        /// Current language Index (default -1 before any language is loaded)，you can use this to choose the highlighted language in a UI list
        /// </summary>
        public static int languageIndex => _languageIndex < 0 ? -1 : _languageIndex;


        public static bool isMetaLoaded => _languageIndex > -2;


        public static bool hasTask => _taskQueue.hasTask;


        /// <summary>
        /// Default 0 before meta is loaded
        /// </summary>
        public static int languageCount => _languageIndex > -2 ? _languages.Length : 0;


        /// <summary>
        /// Start loading meta asynchronously. You can use callback to capture the completion event, or check isMetaLoaded.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadMetaAsync(Action<TaskResult> callback = null, bool forceReload = false)
        {
#if UNITY_EDITOR
            if (Editor.LocalizationEditor.loadExcelsInsteadOfPacks)
            {
                LoadExcelMetaAsync(false, callback, forceReload);
                return;
            }
#endif

            var task = new LoadMetaTask(callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        /// <summary>
        /// Start loading a language asynchronously. You can use callback to capture the completion event, or check languageType.
        /// You must call LoadMetaAsync before calling this.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(string languageType, Action<TaskResult> callback = null, bool forceReload = false)
        {
#if UNITY_EDITOR
            if (Editor.LocalizationEditor.loadExcelsInsteadOfPacks)
            {
                LoadExcelLanguageAsync(languageType, callback, forceReload);
                return;
            }
#endif

            var task = new LoadLanguageTask(languageType, callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        /// <summary>
        /// Start loading a language asynchronously. You can use callback to capture the completion event, or check languageIndex.
        /// You must call this after meta is loaded.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(int languageIndex, Action<TaskResult> callback = null, bool forceReload = false)
        {
#if UNITY_EDITOR
            if (Editor.LocalizationEditor.loadExcelsInsteadOfPacks)
            {
                LoadExcelLanguageAsync(languageIndex, callback, forceReload);
                return;
            }
#endif

            var languageType = _languages[languageIndex].type;
            var task = new LoadLanguageTask(languageType, callback, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        public static void UnloadLanguage()
        {
            _taskQueue.ForEach(task => { if (task.type == TaskType.LoadLanguage) task.Cancel(); });
            if (_languageIndex >= 0)
            {
                _languageIndex = -1;
                _texts = null;

                UpdateContents();
            }
        }


        /// <summary>
        /// Convert languageIndex to languageType. You must call this after meta is loaded.
        /// </summary>
        public static string GetLanguageType(int languageIndex)
        {
            return _languages[languageIndex].type;
        }


        /// <summary>
        /// Convert languageType to languageIndex. You must call this after meta is loaded.
        /// </summary>
        public static int GetLanguageIndex(string languageType)
        {
            return _languageIndices[languageType];
        }


        /// <summary>
        /// Get the specific attribute of a language. You must call this after meta is loaded.
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="attributeName"></param>
        /// <returns>'null' means no such attribute.</returns>
        public static string GetLanguageAttribute(int languageIndex, string attributeName)
        {
            return (attributeName != null && _attributeIndices.TryGetValue(attributeName, out int index))
                ? _languages[languageIndex].attributes[index] : null;
        }


        /// <summary>
        /// Get the specific attribute of a language. You must call this after meta is loaded.
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="attributeName"></param>
        /// <returns>'null' means no such attribute.</returns>
        public static string GetLanguageAttribute(string languageType, string attributeName)
        {
            return (attributeName != null && _attributeIndices.TryGetValue(attributeName, out int index))
                ? _languages[_languageIndices[languageType]].attributes[index] : null;
        }


        public static bool HasText(string textName)
        {
            return textName != null && _textIndices.ContainsKey(textName);
        }


        /// <summary>
        /// Get the specific text of current language. You must call this after a language is loaded.
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="attributeName"></param>
        /// <returns>'null' means no such text.</returns>
        public static string GetText(string textName)
        {
            return (textName != null && _textIndices.TryGetValue(textName, out int index)) ? _texts[index] : null;
        }


        public static void Quit()
        {
            _taskQueue.ForEach(task => task.Cancel());
            _taskQueue.ClearUnprocessed();
            _taskQueue.Dispose();

            _languages = null;
            _languageIndices = null;
            _attributeIndices = null;
            _textIndices = null;
            _texts = null;

            _languageIndex = -2;
        }

    } // struct LocalizationManager

} // namespace UnityExtensions.Localization