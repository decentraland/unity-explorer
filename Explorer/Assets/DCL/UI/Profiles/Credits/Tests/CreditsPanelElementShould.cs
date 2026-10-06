using NUnit.Framework;
using UnityEngine.UIElements;

namespace DCL.UI.Credits.Tests
{
    [TestFixture]
    public class CreditsPanelElementShould
    {
        private CreditsPanelElement credits = null!;

        [SetUp]
        public void SetUp()
        {
            credits = new CreditsPanelElement();
        }

        [Test]
        public void ShowTheAmount()
        {
            //Act
            credits.Credits = "42";

            //Assert
            Assert.AreEqual("42", credits.Q<Label>("Amount").text);
        }

        [Test]
        public void HideUntilShown()
        {
            //Act
            credits.IsShown = false;

            //Assert
            Assert.AreEqual(DisplayStyle.None, credits.style.display.value);

            //Act
            credits.IsShown = true;

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, credits.style.display.value);
        }

        [Test]
        public void IgnoreClicksWhileTopUpIsDisabled()
        {
            //Act
            credits.IsTopUpEnabled = false;

            //Assert
            Assert.AreEqual(PickingMode.Ignore, credits.pickingMode);
            Assert.IsFalse(credits.IsTopUpEnabled);

            //Act
            credits.IsTopUpEnabled = true;

            //Assert
            Assert.AreEqual(PickingMode.Position, credits.pickingMode);
            Assert.IsTrue(credits.IsTopUpEnabled);
        }
    }
}
