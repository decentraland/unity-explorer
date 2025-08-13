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
        private VisualElement? mainPanel;

        internal void Initialize()
        {
            Button toolButton = this.Q<Button>("OpenPanelButton");
            mainPanel = this.Q("Panel");

            Button closeButton = this.Q<Button>("CloseButton");
            closeButton.clicked += () => mainPanel.SetDisplayed(false);
            toolButton.clicked += TogglePanelVisibility;
        }

        public void TogglePanelVisibility() =>
            mainPanel?.SetDisplayed(!IsPanelVisible());

        public bool IsPanelVisible() =>
            mainPanel?.style.display == DisplayStyle.Flex;

        public new class UxmlFactory : UxmlFactory<DebugContainer> { }
    }
}
