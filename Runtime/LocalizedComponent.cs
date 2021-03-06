﻿using UnityEngine;

namespace UnityExtensions.Localization
{
    /// <summary>
    /// LocalizedComponent
    /// </summary>
    [ExecuteAlways]
    public abstract class LocalizedComponent : ScriptableComponent, ILocalizedContent
    {
        int _languageIndex = -1;
        int _contentId = -1;


        public int languageIndex
        {
            get => _languageIndex;
            set
            {
                _languageIndex = value;
                if (value >= 0) UpdateContent();
            }
        }


        /// <summary>
        /// Is this componet added to LocalizationManager?
        /// </summary>
        public bool managed => _contentId >= 0;


        protected virtual void OnEnable()
        {
            _contentId = LocalizationManager.AddContent(this);
        }


        protected virtual void OnDisable()
        {
            LocalizationManager.RemoveContent(_contentId);
            _contentId = -1;
        }


        /// <summary>
        /// Modify the graphic here
        /// </summary>
        protected abstract void UpdateContent();


#if UNITY_EDITOR
        [ContextMenu("Open Localization Window")]
        void OpenLocalizationWindow()
        {
            Editor.LocalizationWindow.ShowWindow();
        }

        [ContextMenu("Open Localization Folder")]
        void OpenLocalizatioFolder()
        {
            Editor.LanguagePacker.ShowSourceFolder();
        }

        [ContextMenu("Reload Localization Meta")]
        void ReloadLocalizationMeta()
        {
            Editor.LocalizationSettings.instance.ReloadMeta();
        }
#endif

    } // class LocalizedComponent

} // UnityExtensions.Localization