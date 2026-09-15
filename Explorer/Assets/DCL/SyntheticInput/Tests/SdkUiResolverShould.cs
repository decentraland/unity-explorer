using DCL.SyntheticInput.UiSimulation;
using NUnit.Framework;

namespace DCL.SyntheticInput.Tests
{
    public class SdkUiResolverShould
    {
        [Test]
        public void NameTheCrdtIdOfACoveringSceneElement()
        {
            // The cover reaches an agent as click_at's blockedByUi: the CRDT id is the only part of it that is an
            // address (ui_click takes it), so a cover naming anything else is not actionable.
            Assert.That(SdkUiResolver.CoverDescription(598), Does.Contain("598"));
        }

        [Test]
        public void StillNameTheSceneWhenNoEntityOwnsThePickedElement()
        {
            Assert.That(SdkUiResolver.CoverDescription(-1), Is.EqualTo("the scene's UI"));
        }
    }
}
