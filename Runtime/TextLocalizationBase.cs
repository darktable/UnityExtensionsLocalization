using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityExtensions.Editor;
#endif

namespace UnityExtensions.Localization
{
    /// <summary>
    /// UI 本地化组件. 使用时，在 UI Text 组件上填写文本名称
    /// </summary>
    public abstract class TextLocalizationBase : ScriptableComponent
    {
        public bool autoUpdateWidth;
        [Indent]
        public float extraWidth;

        public bool autoUpdateHeight;
        [Indent]
        public float extraHeight;


        string _textName;
        int _languageIndex = -1;


        protected abstract string textName { get; }


        void Awake()
        {
            _textName = textName;
#if UNITY_EDITOR
            if (_textName.Trim().Length != _textName.Length)
            {
                Debug.LogError($"Text name can't start/end with white-space characters. ({_textName})");
            }
#endif
        }


        void OnEnable()
        {
            if (_languageIndex != LocalizationManager.languageIndex)
            {
                UpdateLanguage(_languageIndex, LocalizationManager.languageIndex);
            }

            LocalizationManager.onLanguageChanged += UpdateLanguage;
        }


        void OnDisable()
        {
            LocalizationManager.onLanguageChanged -= UpdateLanguage;
        }


        protected virtual void UpdateLanguage(int oldLanguage, int newLanguage)
        {
            _languageIndex = newLanguage;
            LocalizationManager.UpdateUI(_target, _textName);

            if (autoUpdateWidth || autoUpdateHeight)
            {
                var size = this.rectTransform().sizeDelta;
                if (autoUpdateWidth) size.x = _target.preferredWidth + extraWidth;
                if (autoUpdateHeight) size.y = _target.preferredHeight + extraHeight;
                this.rectTransform().sizeDelta = size;
            }
        }


#if UNITY_EDITOR

        [CustomEditor(typeof(TextLocalization), true)]
        public class TextLocalizationEditor : BaseEditor<TextLocalization>
        {
            public override void OnInspectorGUI()
            {
                if (target._target == null) target._target = target.GetComponent<UIText>();

                if (target._target.text.Trim().Length != target._target.text.Length)
                {
                    EditorGUILayout.Space();
                    using (new GUIColorScope(new Color(1, 0.5f, 0.5f)))
                    {
                        if (EditorGUIKit.IndentedButton("Trim Text Name"))
                        {
                            Undo.RecordObject(target._target, "Trim Text Name");
                            target._target.text = target._target.text.Trim();

                            // force update UIText
                            if (target._target.enabled)
                            {
                                target._target.enabled = false;
                                target._target.enabled = true;
                            }
                        }
                    }

                    EditorGUILayout.Space();
                }

                base.OnInspectorGUI();
            }
        }

#endif

    } // class UILocalization

} // UnityExtensions