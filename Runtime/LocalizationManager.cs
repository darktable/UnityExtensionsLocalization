using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

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

            void IQueuedTask<LoadTask>.AfterComplete()
            {
                if (succeeded) Commit();

                asyncTaskCompleted?.Invoke(type, canceled ? TaskResult.Cancel : (succeeded ? TaskResult.Success : TaskResult.Failure), detail);

                if (succeeded) UpdateContents();
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

            public LoadMetaTask(bool forceReload)
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
                    _succeeded = !_canceled;
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

                            if (task.type == TaskType.LoadMeta && task.succeeded)
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

            public LoadLanguageTask(string languageType, bool forceReload)
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
                    _succeeded = !_canceled;
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

                            if (task.detail == _languageType && task.succeeded)
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

            public LoadExcelMetaTask(bool buildCompleted, bool forceReload) : base(forceReload)
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

                        _succeeded = !_canceled;
                    }
                }
            }
        }


        class LoadExcelLanguageTask : LoadLanguageTask
        {
            public LoadExcelLanguageTask(string languageType, bool forceReload) : base(languageType, forceReload)
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

                        _succeeded = !_canceled;
                    }
                }
            }
        }


        public static void LoadExcelMetaAsync(bool buildCompleted = false, bool forceReload = false)
        {
            var task = new LoadExcelMetaTask(buildCompleted, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        public static void LoadExcelLanguageAsync(string languageType, bool forceReload = false)
        {
            var task = new LoadExcelLanguageTask(languageType, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        public static void LoadExcelLanguageAsync(int languageIndex, bool forceReload = false)
        {
            var languageType = _languages[languageIndex].type;
            var task = new LoadExcelLanguageTask(languageType, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }

#endif


        /// <summary>
        /// 异步加载任务完成时触发
        /// </summary>
        public static event Action<TaskType, TaskResult, string> asyncTaskCompleted;


        /// <summary>
        /// 当前语言类型 (default Empty)，可用于存储用户的语言设置
        /// </summary>
        public static string languageType => _languageIndex < 0 ? string.Empty : _languages[_languageIndex].type;


        /// <summary>
        /// 当前语言 Index (default -1)，可用于显示语言列表时高亮当前语言
        /// </summary>
        public static int languageIndex => _languageIndex < 0 ? -1 : _languageIndex;


        public static bool isMetaLoaded => _languageIndex > -2;


        public static bool isLanguageLoaded => _languageIndex >= 0;


        public static bool isLoading => _taskQueue.hasTask;


        /// <summary>
        /// 语言总数，可用于显示语言列表（default 0）
        /// </summary>
        public static int languageCount => _languageIndex > -2 ? _languages.Length : 0;


        /// <summary>
        /// 开始加载本地化的基本信息，应该尽可能早的调用
        /// </summary>
        /// <param name="forceReload"></param>
        public static void LoadMetaAsync(bool forceReload = false)
        {
            var task = new LoadMetaTask(forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        /// <summary>
        /// 开始加载一种语言，必须在 LoadMetaAsync 调用后调用
        /// 用于在开始游戏时通过玩家配置或系统配置设置语言
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(string languageType, bool forceReload = false)
        {
            var task = new LoadLanguageTask(languageType, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        /// <summary>
        /// 开始加载一种语言，必须在 meta 加载完成后调用
        /// 用于在游戏中通过语言列表切换语言
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="forceReload"></param>
        public static void LoadLanguageAsync(int languageIndex, bool forceReload = false)
        {
            var languageType = _languages[languageIndex].type;
            var task = new LoadLanguageTask(languageType, forceReload);
            _taskQueue.Enqueue(task, waitToDisposeThread);
        }


        public static void UnloadLanguage()
        {
            if (_languageIndex >= 0)
            {
                _languageIndex = -1;
                _texts = null;
            }
        }


        /// <summary>
        /// 获取语言类型，必须在 meta 加载完成后调用
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <returns></returns>
        public static string GetLanguageType(int languageIndex)
        {
            return _languages[languageIndex].type;
        }


        /// <summary>
        /// 获取语言 Index，必须在 meta 加载完成后调用
        /// </summary>
        /// <param name="languageType"></param>
        /// <returns></returns>
        public static int GetLanguageIndex(string languageType)
        {
            return _languageIndices[languageType];
        }


        /// <summary>
        /// 获取语言属性，必须在 meta 加载完成后调用
        /// 返回 null 表示属性不存在
        /// </summary>
        /// <param name="languageIndex"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public static string GetLanguageAttribute(int languageIndex, string attributeName)
        {
            return (attributeName != null && _attributeIndices.TryGetValue(attributeName, out int index))
                ? _languages[languageIndex].attributes[index] : null;
        }


        /// <summary>
        /// 获取语言属性，必须在 meta 加载完成后调用
        /// 返回 null 表示属性不存在
        /// </summary>
        /// <param name="languageType"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public static string GetLanguageAttribute(string languageType, string attributeName)
        {
            return (attributeName != null && _attributeIndices.TryGetValue(attributeName, out int index))
                ? _languages[_languageIndices[languageType]].attributes[index] : null;
        }


        /// <summary>
        /// 获取一个唯一的名字对应的文本，必须要在加载一种语言之后调用
        /// 返回 null 表示文本不存在
        /// </summary>
        /// <param name="textName"></param>
        /// <returns></returns>
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