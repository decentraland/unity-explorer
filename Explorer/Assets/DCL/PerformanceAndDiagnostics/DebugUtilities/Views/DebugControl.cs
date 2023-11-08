using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Contains one or two debug visual elements
    /// </summary>
    public class DebugControl : VisualElement
    {
        public VisualElement Left => this.Q<VisualElement>("Left");

        public VisualElement Right => this.Q<VisualElement>("Right");
        public new class UxmlFactory : UxmlFactory<DebugControl> { }
    }
}
