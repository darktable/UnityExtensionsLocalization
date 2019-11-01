using UnityEngine;

namespace UnityExtensions.Localization
{
    public abstract class TextLocalizationComponent : LocalizationComponent
    {
        [SerializeField] string _textName;


        public string textName
        {
            get => _textName;
            set
            {
                if (managed)
                {
                    if (_textName != value)
                    {
                        _textName = value;
                        UpdateContent(LocalizationManager.GetText(value));
                    }
                }
                else _textName = value;
            }
        }


        protected sealed override void UpdateContent(int languageIndex)
        {
            UpdateContent(LocalizationManager.GetText(_textName));
        }


        protected abstract void UpdateContent(string text);

    } // class TextLocalizationComponent

} // UnityExtensions.Localization