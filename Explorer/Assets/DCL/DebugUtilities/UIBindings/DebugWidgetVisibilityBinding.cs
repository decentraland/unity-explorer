using DCL.DebugUtilities.Views;
using Utility.UIToolkit;

namespace DCL.DebugUtilities.UIBindings
{
    /// <summary>
    ///     Allows controlling the visibility of a widget from the external code
    /// </summary>
    public class DebugWidgetVisibilityBinding
    {
        private readonly bool initialValue;

        private DebugWidget debugWidget;

        /// <summary>
        ///     Whether the widget's foldout is toggled
        /// </summary>
        public bool IsExpanded => debugWidget.isExpanded;

        public DebugWidgetVisibilityBinding(bool initialValue)
        {
            this.initialValue = initialValue;
        }

        public void SetVisible(bool visible) =>
            debugWidget.SetDisplayed(visible);

        /// <summary>
        ///     Will be called upon construction with an instance of the widget
        /// </summary>
        internal void Connect(DebugWidget widget)
        {
            debugWidget = widget;
            debugWidget.SetDisplayed(initialValue);
        }
    }
}
