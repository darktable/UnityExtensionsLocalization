using System;

namespace UnityExtensions.Localization
{
    public interface ILocalizedContent
    {
        /// <summary>
        /// default return -1 when get, always apply change when set (could be -1)
        /// </summary>
        int languageIndex { get; set; }
    }


    public partial struct LocalizationManager
    {
        static QuickLinkedList<ILocalizedContent> _contents = new QuickLinkedList<ILocalizedContent>(64);


        public static event Action beforeContentsChange;
        public static event Action afterContentsChange;


        public static int AddContent(ILocalizedContent content)
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