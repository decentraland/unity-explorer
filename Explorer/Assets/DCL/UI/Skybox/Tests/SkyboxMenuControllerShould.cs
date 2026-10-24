using NUnit.Framework;

namespace DCL.UI.Skybox.Tests
{
    public class SkyboxMenuControllerShould
    {
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        public void OpenTimeSliderOnlyWhenNeitherSceneNorDayCycleHoldsIt(bool isRestrictedByScene, bool isDayCycleOn, bool expected)
        {
            // Act
            bool interactable = SkyboxMenuController.IsTimeSliderInteractable(isRestrictedByScene, isDayCycleOn);

            // Assert
            Assert.That(interactable, Is.EqualTo(expected));
        }
    }
}
