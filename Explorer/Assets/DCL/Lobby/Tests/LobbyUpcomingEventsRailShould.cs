using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyUpcomingEventsRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyUpcomingEventCard.uxml";

        private VisualElement section = null!;
        private LobbyUpcomingEventsRail events = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            events = new LobbyUpcomingEventsRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void HideTheSectionWithoutUpcomingEvents()
        {
            //Act
            events.Show(section);
            events.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void ShowOneCardPerEventAndHideTheSurplus()
        {
            //Arrange
            events.Show(section);

            //Act
            events.SetCount(3);
            events.SetCount(1);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(3, events.Cards.Count, "Cards are cloned once and kept for the next fill");
            Assert.AreEqual(DisplayStyle.Flex, Card(0).style.display.value);
            Assert.AreEqual(DisplayStyle.None, Card(1).style.display.value);
            Assert.AreEqual(DisplayStyle.None, Card(2).style.display.value);
            Assert.AreEqual(1, Rail().PageCount);
        }

        [Test]
        public void ReportTheClickedCardAndItsButtonsByIndex()
        {
            //Arrange
            var clicked = -1;
            var interested = -1;
            var calendar = -1;
            var shared = -1;
            events.CardClicked = index => clicked = index;
            events.CardInterestedClicked = index => interested = index;
            events.CardAddToCalendarClicked = index => calendar = index;
            events.CardShareClicked = index => shared = index;
            events.Show(section);
            events.SetCount(3);

            //Act
            Card(2).Clicked!.Invoke();
            Card(1).InterestedClicked!.Invoke();
            Card(0).AddToCalendarClicked!.Invoke();
            Card(1).ShareClicked!.Invoke();

            //Assert
            Assert.AreEqual(2, clicked);
            Assert.AreEqual(1, interested);
            Assert.AreEqual(0, calendar);
            Assert.AreEqual(1, shared);
        }

        [Test]
        public void TintTheCardsOfTheEventsTheUserIsInterestedIn()
        {
            //Arrange
            events.Show(section);
            events.SetCount(2);

            //Act
            Card(0).IsInterested = true;
            Card(1).IsInterested = false;

            //Assert
            Assert.IsTrue(Card(0).ClassListContains("lobby-upcoming-event-card--interested"));
            Assert.IsFalse(Card(1).ClassListContains("lobby-upcoming-event-card--interested"));
        }

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private LobbyUpcomingEventCardElement Card(int index) =>
            (LobbyUpcomingEventCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var upcomingEvents = new VisualElement { name = "UpcomingEvents" };
            upcomingEvents.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 1 });
            return upcomingEvents;
        }
    }
}
