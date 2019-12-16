#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityExtensions.Editor;

namespace UnityExtensions.Localization.Editor
{
    public class LocalizationWindow : SettingsWindow<LocalizationSettings>
    {
        [MenuItem("Window/Localization")]
        public static void ShowWindow()
        {
            var instance = GetWindow<LocalizationWindow>();
            instance.minSize = new Vector2(200, 340);
            instance.titleContent = new GUIContent("Localization");
            instance.Show();
        }

        void OnGUI()
        {
            using (DisabledScope.New(LocalizationManager.hasTask || settings.buildingPacks))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // build packs button
                if (GUILayout.Button("Build Packs")) settings.BuildPacksAsync();

                using (DisabledScope.New(Application.isPlaying))
                {
                    // autoBuildPacksBeforeUnityBuilding
                    settings.autoBuildPacksBeforeUnityBuilding = EditorGUILayout.ToggleLeft(
                        "Auto-build Packs Before Unity Building", settings.autoBuildPacksBeforeUnityBuilding);
                }

                EditorGUILayout.Space();

                using (DisabledScope.New(LocalizationManager.languageIndex < 0))
                {
                    // copy all chars to clipboard
                    if (GUILayout.Button("Copy All Used Characters"))
                    {
                        LocalizationManager.CopyAllUsedCharacters();
                    }
                }

                EditorGUILayout.Space();

                // show folder
                if (GUILayout.Button("Open Localization Folder"))
                {
                    LanguagePacker.ShowSourceFolder();
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // load meta button
                if (GUILayout.Button(LocalizationManager.isMetaLoaded ? "Reload Meta" : "Load Meta"))
                {
                    settings.ReloadMeta();
                }

                using (DisabledScope.New(Application.isPlaying))
                {
                    // autoLoadMeta
                    bool newAutoLoadMetaInEditMode = EditorGUILayout.ToggleLeft("Auto-load Meta in Edit-mode", settings.autoLoadMetaInEditMode);
                    if (newAutoLoadMetaInEditMode != settings.autoLoadMetaInEditMode)
                    {
                        settings.autoLoadMetaInEditMode = newAutoLoadMetaInEditMode;
                        if (settings.autoLoadMetaInEditMode && !LocalizationManager.isMetaLoaded && !LocalizationManager.hasTask)
                        {
                            settings.ReloadMeta();
                        }
                    }

                    using (DisabledScope.New(settings.loadExcelsInsteadOfPacks))
                    {
                        // autoReloadMetaAfterBuildingPacks
                        settings.autoReloadMetaAfterBuildingPacks = EditorGUILayout.ToggleLeft(
                            "Auto-reload Meta After Building Packs", settings.autoReloadMetaAfterBuildingPacks);
                    }
                }

                EditorGUILayout.Space();

                settings.DrawLanguageSelection();

                using (DisabledScope.New(Application.isPlaying))
                {
                    // autoReloadLanguageAfterLoadingMeta
                    settings.autoReloadLanguageAfterLoadingMeta = EditorGUILayout.ToggleLeft(
                        "Auto-reload Language After Loading Meta", settings.autoReloadLanguageAfterLoadingMeta);
                }

                EditorGUILayout.Space();

                // loadExcelsInsteadOfPacks
                settings.loadExcelsInsteadOfPacks = EditorGUILayout.ToggleLeft("Load Excels Instead of Packs", settings.loadExcelsInsteadOfPacks);

                // outputLogs
                settings.outputLogs = EditorGUILayout.ToggleLeft("Output Logs", settings.outputLogs);
            }
        }

    } // LocalizationWindow

} // namespace UnityExtensions.Localization.Editor

#endif // UNITY_EDITOR