using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Container with the scroll view for all possible debug utilities
    /// </summary>
    public class DebugContainer : VisualElement
    {
        internal VisualElement containerRoot => this.Q<VisualElement>("Parent");

        internal void Initialize()
        {
            Button toolButton = this.Q<Button>("OpenPanelButton");
            VisualElement panel = this.Q("Panel");

            Button closeButton = this.Q<Button>("CloseButton");
            closeButton.clicked += () => panel.SetDisplayed(false);

            // toggle
            toolButton.clicked += () => panel.SetDisplayed(panel.style.display == DisplayStyle.None);
        }

        public new class UxmlFactory : UxmlFactory<DebugContainer> { }
    }
}
