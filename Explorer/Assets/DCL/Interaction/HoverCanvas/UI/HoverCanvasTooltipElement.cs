using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Interaction.HoverCanvas.UI
{
    [UxmlElement]
    public partial class HoverCanvasTooltipElement : VisualElement
    {
                private Label hint = null!;

        private bool initialized;

                private Image inputIcon = null!;
                private Label keyName = null!;
                private VisualElement keyRoot = null!;

        // Change-gating: the last (hint, key, icon) triple actually written to the UIToolkit children,
        // so an identical re-apply on a subsequent frame can be skipped.
        private string? lastHintText;
        private string? lastActionKeyText;
        private string? lastIconClass;
        private bool hasApplied;

        // Test seam: count of non-skipped SetData bodies (times SetData actually mutated the UIToolkit children).
        internal int AppliedCount { get; private set; }

        private void Initialize()
        {
            if (initialized)
                return;

            inputIcon = this.Q<Image>("Icon");
            keyName = this.Q<Label>("KeyName");
            hint = this.Q<Label>("Hint");
            keyRoot = this.Q<VisualElement>("KeyRoot");

            initialized = true;
        }

        public void SetData(string? hintText, string? actionKeyText, string? iconClass)
        {
            Initialize();

            // SetData is called for every visible tooltip every presentation frame, almost always with an unchanged triple.
            // Re-applying identical data still dirties layout/repaint (Label.text setter) and rescans the class list, so skip
            // the body when nothing changed. The first apply is never skipped, so initial display state is always established.
            if (hasApplied
                && hintText == lastHintText
                && actionKeyText == lastActionKeyText
                && iconClass == lastIconClass)
                return;

            hasApplied = true;
            lastHintText = hintText;
            lastActionKeyText = actionKeyText;
            lastIconClass = iconClass;
            AppliedCount++;

            if (!string.IsNullOrEmpty(hintText))
            {
                hint.text = hintText;
                hint.SetDisplayed(true);
            }
            else hint.SetDisplayed(false);

            if (!string.IsNullOrEmpty(actionKeyText))
            {
                keyName.text = actionKeyText;
                keyRoot.SetDisplayed(true);
            }
            else keyRoot.SetDisplayed(false);

            if (!string.IsNullOrEmpty(iconClass))
            {
                inputIcon.RemoveSprites();
                inputIcon.AddToClassList(iconClass);
                inputIcon.SetDisplayed(true);
            }
            else inputIcon.SetDisplayed(false);
        }
    }
}
