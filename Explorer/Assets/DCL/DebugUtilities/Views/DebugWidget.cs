using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Widgets corresponds to a single feature scope
    /// </summary>
    public class DebugWidget : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<DebugWidget> { }

        private Foldout foldout;
        private string prefsKey;

        internal bool isExpanded => foldout.value;

        internal void AddElement(VisualElement element)
        {
            foldout.Add(element);
        }

        internal void Initialize(string title)
        {
            foldout = this.Q<Foldout>();
            foldout.text = title;

            prefsKey = ConstructPrefsKey(title);
            foldout.value = PlayerPrefs.GetInt(prefsKey, 0) == 1;

            foldout.RegisterValueChangedCallback(evt => PlayerPrefs.SetInt(prefsKey, evt.newValue ? 1 : 0));
        }

        private static string ConstructPrefsKey(string title) =>
            $"DebugWidget_Foldout_{title}";
    }
}
