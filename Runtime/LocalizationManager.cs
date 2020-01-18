#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_WEBGL)
#define WEB_REQUEST
using UnityEngine.Networking;
#endif

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using AsyncTask = System.Threading.Tasks.Task;

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

        // data in meta file
        static (string type, string[] attributes)[] _languages;
        static Dictionary<string, int> _languageIndices;
        static Dictionary<string, int> _attributeIndices;
        static Dictionary<string, int> _textIndices;

        // data in language file
        static string[] _texts;

        // -2: nothing loaded；-1：meta loaded；>= 0：a language loaded
        static int _state = -2;

        // task queue
        static TaskQueue<LoadTask> _taskQueue = new TaskQueue<LoadTask>();


        // Load Task
        abstract class LoadTask : IQueuedTask<LoadTask>
        {
            Action<TaskResult> _callback;

            public abstract TaskType type { get; }
            public abstract string detail { get; }
            public abstract bool canceled { get; }
            public abstract bool succeeded { get; }

            public abstract bool OnEnqueue(LinkedList<LoadTask> tasks);
            public abstract void OnStart();
            public abstract bool OnUpdate();

            public abstract void Cancel();
            public abstract void Submit();

            public LoadTask(Action<TaskResult> callback)
            {
                _callback = callback;
            }

            void IQueuedTask<LoadTask>.OnComplete()
            {
                if (!canceled && succeeded)
                {
                    Submit();
                    UpdateContents();
                }

                var result = canceled ? TaskResult.Cancel : (succeeded ? TaskResult.Success : TaskResult.Failure);

                _callback?.Invoke(result);
                taskCompleted?.Invoke((type, result, detail));
            }
        }


        // LoadAtPathTask 
        abstract class LoadAtPathTask : LoadTask
        {
            volatile bool _succeeded;
            volatile bool _completed;

            public override bool succeeded => _succeeded;

            public abstract string path { get; }

            public abstract void Load(Stream stream);

#if UNITY_EDITOR
            public abstract void LoadExcel();
#endif

            public LoadAtPathTask(Action<TaskResult> callback) : base(callback) { }

            public override void OnStart()
            {
                if (canceled) return;

#if UNITY_EDITOR
                if (Editor.LocalizationSettings.instance.loadExcelsInsteadOfPacks)
                {
                    AsyncTask.Run(() =>
                    {
                        LoadExcel();
                        _succeeded = true;
                        _completed = true;
                    });
                    return;
                }
#endif

#if WEB_REQUEST
                var request = UnityWebRequest.Get(path);
                request.SendWebRequest().completed += _ =>
                {
                    var data = request.downloadHandler.data;
                    request.Dispose();

                    AsyncTask.Run(() =>
                    {
                        try
                        {
                            Load(new MemoryStream(data, false));
                            _succeeded = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                        _completed = true;
                    });
                };
#else
                AsyncTask.Run(() =>
                {
                    try
                    {
                        Load(new FileStream(path, FileMode.Open, FileAccess.Read));
                        _succeeded = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    _completed = true;
                });
#endif
            }

            public override bool OnUpdate()
            {
                if (canceled) return true;
                return _completed;
            }
        }


        // Load Meta Task
        class LoadMetaTask : LoadAtPathTask
        {
            (string type, string[] attributes)[] _languages;
            Dictionary<string, int> _languageIndices;
            Dictionary<string, int> _attributeIndices;
            Dictionary<string, int> _textIndices;

            volatile bool _canceled;
            bool _forceReload;

            public override TaskType type => TaskType.LoadMeta;
            public override string detail => metaFileName;
            public override bool canceled => _canceled;
            public override string path => $"{Application.streamingAssetsPath}/{targetFolder}/{metaFileName}";

            public LoadMetaTask(Action<TaskResult> callback, bool forceReload) : base(callback)
            {
                _forceReload = forceReload;
            }

            public override bool OnEnqueue(LinkedList<LoadTask> tasks)
            {
                if (!_forceReload)
                {
                    if (_state > -2)
                    {
                        Cancel();
                        return true;
                    }
                    else
                    {
                        int id = tasks.first;
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

            public override void Load(Stream stream)
            {
                using (stream)
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        int attributeCount = reader.ReadInt32();
                        int textCount = reader.ReadInt32();

                        _attributeIndices = new Dictionary<string, int>(attributeCount);
                        for (int i = 0; i < attributeCount; i++)
                        {
                            if (_canceled) return;
                            _attributeIndices.Add(reader.ReadString(), i);
                        }

                        _textIndices = new Dictionary<string, int>(textCount);
                        for (int i = 0; i < textCount; i++)
                        {
                            if (_canceled) return;
                            _textIndices.Add(reader.ReadString(), i);
                        }

                        _languages = new (string, string[])[reader.ReadInt32()];
                        _languageIndices = new Dictionary<string, int>(_languages.Length);
                        for (int i = 0; i < _languages.Length; i++)
                        {
                            if (_canceled) return;
                            _languages[i].type = reader.ReadString();
                            _languageIndices.Add(_languages[i].type, i);
                            _languages[i].attributes = new string[attributeCount];
                            for (int j = 0; j < attributeCount; j++)
                            {
                                if (_canceled) return;
                                _languages[i].attributes[j] = reader.ReadString();
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR
            public override void LoadExcel()
            {
                Editor.LanguagePacker.ReadExcels();
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
                }
            }
#endif

            public override void Cancel()
            {
                _canceled = true;
            }

            public override void Submit()
            {
                LocalizationManager._languages = _languages;
                LocalizationManager._languageIndices = _languageIndices;
                LocalizationManager._attributeIndices = _attributeIndices;
                LocalizationManager._textIndices = _textIndices;
                _texts = null;
                _state = -1;
            }
        }


        // Load Language Task
        class LoadLanguageTask : LoadAtPathTask
        {
            string _languageType;
            string[] _texts;

            volatile bool _canceled;
            bool _forceReload;

            public override TaskType type => TaskType.LoadLanguage;
            public override string detail => _languageType;
            public override bool canceled => _canceled;
            public override string path => $"{Application.streamingAssetsPath}/{targetFolder}/{_languageType}";

            public LoadLanguageTask(string languageType, Action<TaskResult> callback, bool forceReload) : base(callback)
            {
                _languageType = languageType;
                _forceReload = forceReload;
            }

            public override bool OnEnqueue(LinkedList<LoadTask> tasks)
            {
                if (!_forceReload)
                {
                    if (_state >= 0 && _languages[_state].type == _languageType)
                    {
                        Cancel();
                        return true;
                    }
                    else
                    {
                        int id = tasks.first;
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

            public override void Load(Stream stream)
            {
                using (stream)
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        _texts = new string[reader.ReadInt32()];
                        for (int i = 0; i < _texts.Length; i++)
                        {
                            if (_canceled) return;
                            _texts[i] = reader.ReadString();
                        }
                    }
                }
            }

#if UNITY_EDITOR
            public override void LoadExcel()
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
                }
            }
#endif

            public override void Cancel()
            {
                _canceled = true;
            }

            public override void Submit()
            {
                LocalizationManager._texts = _texts;
                _state = _languageIndices[_languageType];
            }
        }


        /// <summary>
        /// Trigger on any async task completed.
        /// Every xxxAsync call corresponds a taskCompleted callback.
        /// </summary>
        public static event Action<(TaskType type, TaskResult result, string detail)> taskCompleted;


        /// <summary>
        /// Current language type (default Empty before any language is loaded)，you can save this in user data
        /// </summary>
        public static string languageType => _state < 0 ? string.Empty : _languages[_state].type;


        /// <summary>
        /// Current language Index (default -1 before any language is loaded)，you can use this to choose the highlighted language in a UI list
        /// </summary>
        public static int languageIndex => _state < 0 ? -1 : _state;


        public static bool isMetaLoaded => _state > -2;


        public static bool hasTask => _taskQueue.hasTask;


        /// <summary>
        /// Default 0 before meta is loaded
        /// </summary>
        public static int languageCount => _state > -2 ? _languages.Length : 0;


        /// <summary>
        /// Start loading meta asynchronously. You can use callback to capture the completion event, or check isMetaLoaded.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadMetaAsync(Action<TaskResult> callback = null, bool forceReload = false)
        {
            _taskQueue.Enqueue(new LoadMetaTask(callback, forceReload));
        }


        /// <summary>
        /// Start loading a language asynchronously. You can use callback to capture the completion event, or check languageType.
        /// You must call LoadMetaAsync before calling this.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(string languageType, Action<TaskResult> callback = null, bool forceReload = false)
        {
            _taskQueue.Enqueue(new LoadLanguageTask(languageType, callback, forceReload));
        }


        /// <summary>
        /// Start loading a language asynchronously. You can use callback to capture the completion event, or check languageIndex.
        /// You must call this after meta is loaded.
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(int languageIndex, Action<TaskResult> callback = null, bool forceReload = false)
        {
            var languageType = _languages[languageIndex].type;
            _taskQueue.Enqueue(new LoadLanguageTask(languageType, callback, forceReload));
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

        /// <summary>
        /// Wait all tasks to be completed.
        /// </summary>
        public static void Wait()
        {
            _taskQueue.Wait();
        }

#if UNITY_EDITOR

        static string GetAllUsedCharacters()
        {
            HashSet<char> chars = new HashSet<char>();
            foreach (var text in _texts)
            {
                foreach (var c in text)
                {
                    chars.Add(c);
                }
            }

            foreach (var c in _languages[_state].attributes[_attributeIndices[languageName]])
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


        public static void CopyAllUsedCharacters()
        {
            var editor = new TextEditor();
            editor.text = GetAllUsedCharacters();
            editor.SelectAll();
            editor.Copy();
        }


        public static void UnloadLanguage()
        {
            foreach (var task in _taskQueue.tasks)
            {
                if (task.type == TaskType.LoadLanguage) task.Cancel();
            }

            if (_state >= 0)
            {
                _state = -1;
                _texts = null;
                UpdateContents();
            }
        }


        public static void Quit()
        {
            foreach (var task in _taskQueue.tasks)
            {
                task.Cancel();
            }

            _taskQueue.RemoveUnprocessed();

            _languages = null;
            _languageIndices = null;
            _attributeIndices = null;
            _textIndices = null;
            _texts = null;

            _state = -2;
        }

#endif

    } // struct LocalizationManager

} // namespace UnityExtensions.Localization