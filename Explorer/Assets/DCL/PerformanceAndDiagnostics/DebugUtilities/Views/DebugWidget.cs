using DCL.Prefs;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Widgets corresponds to a single feature scope
    /// </summary>
    public class DebugWidget : VisualElement
    {
        private Foldout foldout;
        private string prefsKey;

        internal bool isExpanded => foldout.value;

        internal void Initialize(string title, string? foldKey = null)
        {
            foldout = this.Q<Foldout>();
            foldout.text = title;

            prefsKey = ConstructPrefsKey(foldKey ?? title);
            foldout.value = DCLPlayerPrefs.GetInt(prefsKey, 0) == 1;

            foldout.RegisterValueChangedCallback(evt => DCLPlayerPrefs.SetInt(prefsKey, evt.newValue ? 1 : 0));
        }

        internal void AddElement(VisualElement element)
        {
            foldout.Add(element);
        }

        internal void RemoveElementIfAttached(VisualElement element)
        {
            if (foldout.Contains(element))
                foldout.Remove(element);
        }

        private static string ConstructPrefsKey(string title) =>
            string.Format(DCLPrefKeys.DEBUG_WIDGET_FOLDOUT, title);

        public new class UxmlFactory : UxmlFactory<DebugWidget> { }
    }
}
