using System;

namespace UnityExtensions.Localization
{
    public interface ILocalizationContent
    {
        /// <summary>
        /// default return -1 when get, always apply change when set
        /// </summary>
        int languageIndex { get; set; }
    }


    public partial struct LocalizationManager
    {
        static QuickLinkedList<ILocalizationContent> _contents = new QuickLinkedList<ILocalizationContent>(64);


        public static event Action beforeContentsChange;
        public static event Action afterContentsChange;


        public static int AddContent(ILocalizationContent content)
        {
            if (content.languageIndex != languageIndex)
            {
                content.languageIndex = languageIndex;
            }

            return _contents.AddLast(content);
        }


        public static void RemoveContent(int contentId)
        {
            _contents.Remove(contentId);
        }


        static void UpdateContents()
        {
            beforeContentsChange?.Invoke();

            foreach (var item in _contents)
            {
                item.languageIndex = languageIndex;
            }

            afterContentsChange?.Invoke();
        }
    }

} // UnityExtensions.Localization