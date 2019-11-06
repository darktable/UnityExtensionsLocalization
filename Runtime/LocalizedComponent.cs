using UnityEngine;

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


        protected abstract void UpdateContent();


#if UNITY_EDITOR
        [ContextMenu("Open Localization Window")]
        void OpenLocalizationWindow()
        {
            Editor.LocalizationEditor.ShowWindow();
        }
#endif

    } // class LocalizedComponent

} // UnityExtensions.Localization