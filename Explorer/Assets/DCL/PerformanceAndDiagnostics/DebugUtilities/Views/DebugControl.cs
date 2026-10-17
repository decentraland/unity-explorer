using DCL.Utilities.Extensions;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    /// <summary>
    ///     Contains one or two debug visual elements
    /// </summary>
    [UxmlElement]
    public partial class DebugControl : VisualElement
    {
        public VisualElement Left => this.Q<VisualElement>("Left").EnsureNotNull();

        public VisualElement Right => this.Q<VisualElement>("Right").EnsureNotNull();
    }
}
