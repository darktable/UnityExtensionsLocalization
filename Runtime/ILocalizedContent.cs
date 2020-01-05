using System;

namespace UnityExtensions.Localization
{
    public interface ILocalizedContent
    {
        /// <summary>
        /// default return -1, always apply change when set (could be -1)
        /// </summary>
        int languageIndex { get; set; }
    }


    public partial struct LocalizationManager
    {
        static LinkedList<ILocalizedContent> _contents = new LinkedList<ILocalizedContent>(64);


        public static event Action beforeContentsChange;
        public static event Action afterContentsChange;


        /// <summary>
        /// Add a localized content to the LocalizationManager, return an id of this content.
        /// </summary>
        public static int AddContent(ILocalizedContent content)
        {
            if (content.languageIndex != languageIndex)
            {
                content.languageIndex = languageIndex;
            }

            return _contents.AddLast(content);
        }


        /// <summary>
        /// Remove a localized content from the LocalizationManager, use the id returned by AddContent
        /// </summary>
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