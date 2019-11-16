#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityExtensions.Editor;
using System.Threading.Tasks;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace UnityExtensions.Localization.Editor
{
    public class LocalizationEditor : SerializableWindowSingleton<LocalizationEditor>
    {
        [SerializeField] bool _autoBuildPacksBeforeUnityBuilding;
        [SerializeField] bool _autoLoadMetaInEditMode;
        [SerializeField] bool _autoReloadMetaAfterBuildingPacks;
        [SerializeField] bool _autoReloadLanguageAfterLoadingMeta;
        [SerializeField] bool _loadExcelsInsteadOfPacks;
        [SerializeField] bool _outputLogs;

        [SerializeField] string _languageType;


        const string saveFile = ".localization";

        static string[] _noneLanguage = { "(None Language)" };
        static string[] _languages = _noneLanguage;
        static Task<bool> _buildTask;


        class PreprocessBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                if (instance._autoBuildPacksBeforeUnityBuilding)
                {
                    if (_buildTask == null) BuildPacks();
                    else _buildTask.Wait();
                }
            }
        }


        [MenuItem("Window/Localization")]
        public static void ShowWindow()
        {
            instance.minSize = new Vector2(200, 340);
            instance.titleContent = new GUIContent("Localization");
            instance.Show();
        }


        public static void ShowFolder()
        {
            Directory.CreateDirectory(LanguagePacker.sourceFolder);
            Application.OpenURL(LanguagePacker.sourceFolder);
        }


        public static string GetLanguageLabel(int index)
        {
            return _languages[index];
        }


        public static bool loadExcelsInsteadOfPacks => instance._loadExcelsInsteadOfPacks;


        [InitializeOnLoadMethod]
        static void Init()
        {
            LocalizationManager.asyncTaskCompleted += TaskCompleted;

            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && instance._autoLoadMetaInEditMode)
                {
                    ReloadMeta();
                }

                EditorApplication.playModeStateChanged += mode =>
                {
                    if (mode == PlayModeStateChange.EnteredEditMode)
                    {
                        if (instance._autoLoadMetaInEditMode)
                        {
                            ReloadMeta();
                        }
                        else
                        {
                            LocalizationManager.Quit();
                            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        }
                    }
                };
            };
        }


        static void ResetLanguageList()
        {
            _languages = new string[LocalizationManager.languageCount + 1];
            for (int i = 0; i < LocalizationManager.languageCount; i++)
            {
                _languages[i] = $"{LocalizationManager.GetLanguageAttribute(i, "LanguageName")} ({LocalizationManager.GetLanguageType(i)})";
            }
            _languages[_languages.Length - 1] = _noneLanguage[0];
        }


        static void ReloadMeta(bool buildCompleted = false)
        {
            if (!instance._loadExcelsInsteadOfPacks) LocalizationManager.LoadMetaAsync(true);
            else LocalizationManager.LoadExcelMetaAsync(buildCompleted, true);

            if (!EditorApplication.isPlayingOrWillChangePlaymode && instance._autoReloadLanguageAfterLoadingMeta && !string.IsNullOrEmpty(instance._languageType))
            {
                if (!instance._loadExcelsInsteadOfPacks) LocalizationManager.LoadLanguageAsync(instance._languageType, true);
                else LocalizationManager.LoadExcelLanguageAsync(instance._languageType, true);
            }
        }


        static bool BuildPacks()
        {
            return LanguagePacker.ReadExcels() && LanguagePacker.WritePacks(!instance._loadExcelsInsteadOfPacks);
        }


        static void BuildCompleted(Task<bool> task)
        {
            _buildTask = null;

            AssetDatabase.Refresh();

            if (!EditorApplication.isPlayingOrWillChangePlaymode && task.Result)
            {
                if (LocalizationManager.isMetaLoaded && instance._autoReloadMetaAfterBuildingPacks)
                {
                    ReloadMeta(true);
                }
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }


        static void TaskCompleted((TaskType, TaskResult, string) data)
        {
            var (type, result, detail) = data;
            if (instance._outputLogs)
            {
                var message = $"[Localization] Task: {type}, Result: {result}, Detail: {detail}.";
                if (result == TaskResult.Success) Debug.Log(message);
                if (result == TaskResult.Cancel) Debug.LogWarning(message);
                if (result == TaskResult.Failure) Debug.LogError(message);
            }

            if (type == TaskType.LoadMeta)
            {
                ResetLanguageList();
            }
            if (type == TaskType.LoadLanguage)
            {
                if (EditorUtilities.playMode == PlayModeStateChange.EnteredEditMode)
                    instance._languageType = LocalizationManager.languageType;
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }


        public static void DrawLanguageSelection()
        {
            // select language
            int languageIndex = (LocalizationManager.languageIndex < 0) ? (_languages.Length - 1) : LocalizationManager.languageIndex;

            int index = EditorGUILayout.Popup(GUIContent.none, languageIndex, _languages);
            if (index != languageIndex)
            {
                if (index == _languages.Length - 1)
                {
                    LocalizationManager.UnloadLanguage();
                    TaskCompleted((TaskType.LoadLanguage, TaskResult.Success, _noneLanguage[0]));
                }
                else
                {
                    if (!instance._loadExcelsInsteadOfPacks) LocalizationManager.LoadLanguageAsync(index);
                    else LocalizationManager.LoadExcelLanguageAsync(index);
                }
            }
        }


        static void CopyAllUsedCharacters()
        {
            var editor = new TextEditor();
            editor.text = LocalizationManager.GetAllUsedCharacters();
            editor.SelectAll();
            editor.Copy();
        }


        void OnGUI()
        {
            using (DisabledScope.New(LocalizationManager.hasTask || _buildTask != null))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // build packs button
                if (GUILayout.Button("Build Packs"))
                {
                    _buildTask = TaskMonitor.Add(BuildPacks, BuildCompleted);
                }

                using (DisabledScope.New(Application.isPlaying))
                {
                    // _autoBuildPacksBeforeUnityBuilding
                    _autoBuildPacksBeforeUnityBuilding = EditorGUILayout.ToggleLeft(
                        "Auto-build Packs Before Unity Building", _autoBuildPacksBeforeUnityBuilding);
                }

                EditorGUILayout.Space();

                using (DisabledScope.New(LocalizationManager.languageIndex < 0))
                {
                    // copy all chars to clipboard
                    if (GUILayout.Button("Copy All Used Characters"))
                    {
                        CopyAllUsedCharacters();
                    }
                }

                EditorGUILayout.Space();

                // show folder
                if (GUILayout.Button("Open Localization Folder"))
                {
                    ShowFolder();
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // load meta button
                if (GUILayout.Button(LocalizationManager.isMetaLoaded ? "Reload Meta" : "Load Meta"))
                {
                    ReloadMeta();
                }

                using (DisabledScope.New(Application.isPlaying))
                {
                    // _autoLoadMeta
                    bool newAutoLoadMetaInEditMode = EditorGUILayout.ToggleLeft("Auto-load Meta in Edit-mode", _autoLoadMetaInEditMode);
                    if (newAutoLoadMetaInEditMode != _autoLoadMetaInEditMode)
                    {
                        _autoLoadMetaInEditMode = newAutoLoadMetaInEditMode;
                        if (_autoLoadMetaInEditMode && !LocalizationManager.isMetaLoaded && !LocalizationManager.hasTask)
                        {
                            ReloadMeta();
                        }
                    }

                    // _autoReloadMetaAfterBuildingPacks
                    _autoReloadMetaAfterBuildingPacks = EditorGUILayout.ToggleLeft(
                        "Auto-reload Meta After Building Packs", _autoReloadMetaAfterBuildingPacks);
                }

                EditorGUILayout.Space();

                DrawLanguageSelection();

                using (DisabledScope.New(Application.isPlaying))
                {
                    // _autoReloadLanguageAfterLoadingMeta
                    _autoReloadLanguageAfterLoadingMeta = EditorGUILayout.ToggleLeft(
                        "Auto-reload Language After Loading Meta", _autoReloadLanguageAfterLoadingMeta);
                }

                EditorGUILayout.Space();

                // _loadExcelsInsteadOfPacks
                _loadExcelsInsteadOfPacks = EditorGUILayout.ToggleLeft("Load Excels Instead of Packs", _loadExcelsInsteadOfPacks);

                // _outputLogs
                _outputLogs = EditorGUILayout.ToggleLeft("Output Logs", _outputLogs);
            }
        }


        protected override void Save(string data)
        {
            try
            {
                Directory.CreateDirectory(LanguagePacker.sourceFolder);
                using (var stream = new FileStream($"{LanguagePacker.sourceFolder}/{saveFile}", FileMode.Create, FileAccess.Write))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(data);
                    }
                }
            }
            catch { }
        }


        protected override string Load()
        {
            try
            {
                using (var stream = File.Open($"{LanguagePacker.sourceFolder}/{saveFile}", FileMode.Open, FileAccess.Read))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        return reader.ReadString();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR