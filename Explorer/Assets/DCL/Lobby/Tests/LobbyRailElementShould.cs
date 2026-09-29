using NUnit.Framework;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    public class LobbyRailElementShould
    {
        private LobbyRailElement rail = null!;

        [SetUp]
        public void SetUp()
        {
            rail = new LobbyRailElement { CardsPerPage = 3 };
        }

        [Test]
        public void KeepTheCardsApartFromItsChrome()
        {
            // Act
            rail.Add(new VisualElement());
            rail.Add(new VisualElement());

            // Assert
            Assert.AreEqual(2, rail.childCount);
            Assert.AreEqual(2, rail.hierarchy.childCount, "Only the viewport and the dots hang from the rail itself");
        }

        [Test]
        public void ShowOneDotPerPage()
        {
            // Act
            ShowCards(7);

            // Assert
            Assert.AreEqual(3, rail.PageCount);
            Assert.AreEqual(3, VisibleDots());
        }

        [Test]
        public void HideTheDotsWhileEverythingFitsInOnePage()
        {
            // Act
            ShowCards(3);

            // Assert
            Assert.AreEqual(1, rail.PageCount);
            Assert.AreEqual(0, VisibleDots());
        }

        [Test]
        public void HideTheArrowThatHasNoPageLeftToGoTo()
        {
            // Arrange
            ShowCards(7);

            // Assert
            Assert.AreEqual(DisplayStyle.None, Arrow("Previous").style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Arrow("Next").style.display.value);

            // Act
            rail.SnapTo(2);

            // Assert
            Assert.AreEqual(2, rail.CurrentPage);
            Assert.AreEqual(DisplayStyle.Flex, Arrow("Previous").style.display.value);
            Assert.AreEqual(DisplayStyle.None, Arrow("Next").style.display.value);
        }

        [Test]
        public void ClampThePageToTheOnesTheCardsFill()
        {
            // Arrange
            ShowCards(7);

            // Act
            rail.SnapTo(10);

            // Assert
            Assert.AreEqual(2, rail.CurrentPage);

            // Act
            rail.SnapTo(-1);

            // Assert
            Assert.AreEqual(0, rail.CurrentPage);
        }

        [Test]
        public void RewindWhenTheCardsChange()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(2);

            // Act
            rail.SetCardCount(4);

            // Assert
            Assert.AreEqual(0, rail.CurrentPage);
            Assert.AreEqual(2, VisibleDots());
        }

        [Test]
        public void KeepThePageWhenTheCardsChangeWithoutRewinding()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(1);

            // Act
            rail.SetCardCount(5, rewind: false);

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
            Assert.AreEqual(2, VisibleDots());
        }

        [Test]
        public void FallBackToTheLastPageTheCardsStillFillWithoutRewinding()
        {
            // Arrange
            ShowCards(7);
            rail.SnapTo(2);

            // Act
            rail.SetCardCount(4, rewind: false);

            // Assert
            Assert.AreEqual(1, rail.CurrentPage);
        }

        private void ShowCards(int count)
        {
            for (var i = 0; i < count; i++)
                rail.Add(new VisualElement());

            rail.SetCardCount(count);
        }

        private VisualElement Arrow(string name) =>
            rail.Q<Button>(name);

        private int VisibleDots()
        {
            VisualElement dots = rail.Q("Dots");
            var visible = 0;

            for (var i = 0; i < dots.childCount; i++)
                if (dots[i].style.display.value == DisplayStyle.Flex)
                    visible++;

            return visible;
        }
    }
}
