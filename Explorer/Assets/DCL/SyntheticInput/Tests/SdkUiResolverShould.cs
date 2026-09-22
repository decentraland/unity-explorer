using DCL.SyntheticInput.UiSimulation;
using NUnit.Framework;

namespace DCL.SyntheticInput.Tests
{
    public class SdkUiResolverShould
    {
        [Test]
        public void NameTheCrdtIdOfACoveringSceneElement()
        {
            Assert.That(SdkUiResolver.CoverDescription(598), Does.Contain("598"));
        }

        [Test]
        public void StillNameTheSceneWhenNoEntityOwnsThePickedElement()
        {
            Assert.That(SdkUiResolver.CoverDescription(-1), Is.EqualTo("the scene's UI"));
        }
    }
}
