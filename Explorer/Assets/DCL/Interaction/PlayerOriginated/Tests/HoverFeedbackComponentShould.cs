using DCL.Interaction.PlayerOriginated.Components;
using NUnit.Framework;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class HoverFeedbackComponentShould
    {
        private const int MAX_TOOLTIPS = 2;

        [Test]
        public void DropTooltipsBeyondTheMaximum()
        {
            var component = new HoverFeedbackComponent(MAX_TOOLTIPS);
            var first = new HoverFeedbackComponent.Tooltip("first", new UnityEngine.InputSystem.InputAction("first"));
            var second = new HoverFeedbackComponent.Tooltip("second", new UnityEngine.InputSystem.InputAction("second"));
            var third = new HoverFeedbackComponent.Tooltip("third", new UnityEngine.InputSystem.InputAction("third"));

            component.Add(first);
            component.Add(second);
            component.Add(third);

            Assert.That(component.Tooltips.Count, Is.EqualTo(MAX_TOOLTIPS));
            Assert.That(component.Tooltips[0].Text, Is.EqualTo("first"));
            Assert.That(component.Tooltips[1].Text, Is.EqualTo("second"));
        }
    }
}
