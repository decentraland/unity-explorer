using DCL.Interaction.HoverCanvas.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DCL.Interaction.HoverCanvas.Tests
{
    public class HoverCanvasTooltipElementShould
    {
        private static HoverCanvasTooltipElement CreateElement()
        {
            var element = new HoverCanvasTooltipElement();

            // Children the element resolves in Initialize() via this.Q<>(name).
            element.Add(new Image { name = "Icon" });
            element.Add(new Label { name = "KeyName" });
            element.Add(new Label { name = "Hint" });
            element.Add(new VisualElement { name = "KeyRoot" });

            return element;
        }

        // SetData must apply its UIToolkit writes (Label.text setters, RemoveSprites + AddToClassList class-list scans,
        // SetDisplayed) at most ONCE while the displayed (hint, key, icon) triple is unchanged, and re-apply when it
        // changes. AppliedCount is bumped once per non-skipped SetData body.
        [Test]
        public void NotReapplyUiWhenDataUnchanged()
        {
            HoverCanvasTooltipElement element = CreateElement();

            element.SetData("Interact", "E", "sprite-e");

            const int FRAMES = 600;

            for (var i = 0; i < FRAMES; i++)
                element.SetData("Interact", "E", "sprite-e");

            Assert.AreEqual(1, element.AppliedCount,
                "Unchanged tooltip data must not re-dirty UIToolkit every frame.");

            // A genuine change must re-apply exactly once more (proves the gate is state-driven).
            element.SetData("Grab", "F", "sprite-f");
            Assert.AreEqual(2, element.AppliedCount);
        }
    }
}
