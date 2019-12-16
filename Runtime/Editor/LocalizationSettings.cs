#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityExtensions.Editor;
using System.Threading.Tasks;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UnityExtensions.Localization.Editor
{
    [System.Serializable]
    public class LocalizationSettings : EditorSettings<LocalizationSettings>
    {
        public bool autoBuildPacksBeforeUnityBuilding = true;
        public bool autoLoadMetaInEditMode = true;
        public bool autoReloadMetaAfterBuildingPacks;
        public bool autoReloadLanguageAfterLoadingMeta = true;
        public bool loadExcelsInsteadOfPacks = true;
        public bool outputLogs;
        public string languageType;

        static string[] _noneLanguage = { "(None Language)" };
        string[] _languages = _noneLanguage;
        Task<bool> _buildTask;

        public bool buildingPacks => _buildTask != null;

        class PreprocessBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                if (instance._buildTask != null) instance._buildTask.Wait();
                else
                {
                    if (instance.autoBuildPacksBeforeUnityBuilding)
                    {
                        instance.BuildPacks();
                    }
                }
            }
        }

        [InitializeOnLoadMethod]
        static void Init()
        {
            LocalizationManager.asyncTaskCompleted += instance.TaskCompleted;

            if (!EditorApplication.isPlayingOrWillChangePlaymode && instance.autoLoadMetaInEditMode)
            {
                instance.ReloadMeta();
            }

            EditorApplication.playModeStateChanged += mode =>
            {
                if (mode == PlayModeStateChange.EnteredEditMode)
                {
                    if (instance.autoLoadMetaInEditMode)
                    {
                        instance.ReloadMeta();
                    }
                    else
                    {
                        LocalizationManager.Quit();
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    }
                }
            };
        }

        public string GetLanguageLabel(int index)
        {
            return _languages[index];
        }

        void TaskCompleted((TaskType, TaskResult, string) data)
        {
            var (type, result, detail) = data;
            if (outputLogs)
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
                    languageType = LocalizationManager.languageType;
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        void ResetLanguageList()
        {
            _languages = new string[LocalizationManager.languageCount + 1];
            for (int i = 0; i < LocalizationManager.languageCount; i++)
            {
                _languages[i] = $"{LocalizationManager.GetLanguageAttribute(i, "LanguageName")} ({LocalizationManager.GetLanguageType(i)})";
            }
            _languages[_languages.Length - 1] = _noneLanguage[0];
        }

        bool BuildPacks()
        {
            return LanguagePacker.ReadExcels() && LanguagePacker.WritePacks();
        }

        public void ReloadMeta()
        {
            LocalizationManager.LoadMetaAsync(null, true);

            if (!EditorApplication.isPlayingOrWillChangePlaymode && autoReloadLanguageAfterLoadingMeta && !string.IsNullOrEmpty(languageType))
            {
                LocalizationManager.LoadLanguageAsync(languageType, null, true);
            }
        }

        public void DrawLanguageSelection()
        {
            int languageIndex = (LocalizationManager.languageIndex < 0) ? (_languages.Length - 1) : LocalizationManager.languageIndex;

            int index = EditorGUILayout.Popup(GUIContent.none, languageIndex, _languages);
            if (index != languageIndex)
            {
                if (index == _languages.Length - 1)
                {
                    LocalizationManager.UnloadLanguage();
                    TaskCompleted((TaskType.LoadLanguage, TaskResult.Success, _noneLanguage[0]));
                }
                else LocalizationManager.LoadLanguageAsync(index);
            }
        }

        public void BuildPacksAsync()
        {
            _buildTask = TaskMonitor.Add(BuildPacks, BuildCompleted);
        }

        void BuildCompleted(Task<bool> task)
        {
            _buildTask = null;

            AssetDatabase.Refresh();

            if (!EditorApplication.isPlayingOrWillChangePlaymode && task.Result)
            {
                if (!loadExcelsInsteadOfPacks && LocalizationManager.isMetaLoaded && autoReloadMetaAfterBuildingPacks)
                {
                    ReloadMeta();
                }
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

    } // LocalizationSettings

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR