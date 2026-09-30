using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyPlacesRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyPlaceCard.uxml";

        private VisualElement section = null!;
        private LobbyPlacesRail places = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            places = new LobbyPlacesRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void HideTheSectionWithoutPlaces()
        {
            //Act
            places.Show(section);
            places.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual(0, places.Count);
        }

        [Test]
        public void ShowOneCardPerPlaceAndHideTheSurplus()
        {
            //Arrange
            places.Show(section);

            //Act
            places.SetCount(3);
            places.SetCount(1);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(1, places.Count);
            Assert.AreEqual(3, places.Cards.Count, "Cards are cloned once and kept for the next fill");
            Assert.AreEqual(3, Rail().childCount);
            Assert.AreEqual(DisplayStyle.Flex, Card(0).style.display.value);
            Assert.AreEqual(DisplayStyle.None, Card(1).style.display.value);
            Assert.AreEqual(DisplayStyle.None, Card(2).style.display.value);
        }

        [Test]
        public void ShowOneDotPerPageOfThreeCards()
        {
            //Arrange
            places.Show(section);

            //Act
            places.SetCount(7);

            //Assert
            Assert.AreEqual(3, Rail().PageCount);
            Assert.AreEqual(3, VisibleDots());
            Assert.AreEqual(0, Rail().CurrentPage);
        }

        [Test]
        public void ReportTheClickedCardByIndex()
        {
            //Arrange
            var clicked = -1;
            var jumpedIn = -1;
            places.CardClicked = index => clicked = index;
            places.CardJumpInClicked = index => jumpedIn = index;
            places.Show(section);
            places.SetCount(2);

            //Act
            Card(1).Clicked!.Invoke();
            Card(0).JumpInClicked!.Invoke();

            //Assert
            Assert.AreEqual(1, clicked);
            Assert.AreEqual(0, jumpedIn);
        }

        [Test]
        public void FillAPlainRowWhenTheSectionHasNoRail()
        {
            //Arrange
            var row = new VisualElement { name = "Cards" };
            var rowSection = new VisualElement { name = "RecentPlaces" };
            rowSection.Add(new Label { name = "Title", text = "RECENT PLACES" });
            rowSection.Add(row);

            //Act
            places.Show(rowSection);
            places.SetCount(3);
            places.SetCount(2);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, rowSection.style.display.value);
            Assert.AreEqual(3, row.childCount, "The cards land in the row itself");
            Assert.AreEqual(DisplayStyle.Flex, row[0].style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, row[1].style.display.value);
            Assert.AreEqual(DisplayStyle.None, row[2].style.display.value);
            Assert.IsNull(rowSection.Q<LobbyRailElement>(), "A plain row gets no rail chrome");
        }

        [Test]
        public void MoveTheCardsIntoTheNextSection()
        {
            //Arrange
            places.Show(section);
            places.SetCount(1);
            LobbyPlaceCardElement card = Card(0);
            places.Hide();
            VisualElement nextSection = CreateSection();

            //Act
            places.Show(nextSection);

            //Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual(0, Rail().childCount);
            Assert.AreSame(card, nextSection.Q<LobbyRailElement>()[0]);
            Assert.AreEqual(DisplayStyle.None, card.style.display.value, "A moved card stays hidden until the next fill");
            Assert.AreEqual(0, places.Count);
        }

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private LobbyPlaceCardElement Card(int index) =>
            (LobbyPlaceCardElement)Rail()[index];

        private int VisibleDots()
        {
            VisualElement dots = Rail().Q("Dots");
            var visible = 0;

            for (var i = 0; i < dots.childCount; i++)
                if (dots[i].style.display.value == DisplayStyle.Flex)
                    visible++;

            return visible;
        }

        // The title and the rail of a places section of the document, without its stylesheet
        private static VisualElement CreateSection()
        {
            var placesSection = new VisualElement { name = "RecentPlaces" };
            placesSection.Add(new Label { name = "Title", text = "RECENT PLACES" });
            placesSection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 3 });
            return placesSection;
        }
    }
}
