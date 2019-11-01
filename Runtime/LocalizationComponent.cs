using UnityEngine;

namespace UnityExtensions.Localization
{
    /// <summary>
    /// LocalizationComponent
    /// </summary>
    [ExecuteAlways]
    public abstract class LocalizationComponent : ScriptableComponent, ILocalizationContent
    {
        int _languageIndex = -1;
        int _contentId = -1;


        public int languageIndex
        {
            get => _languageIndex;
            set
            {
                _languageIndex = value;
                UpdateContent(value);
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


        protected abstract void UpdateContent(int languageIndex);

    } // class LocalizationComponent

} // UnityExtensions.Localization