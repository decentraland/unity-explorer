using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLiveEventsRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyLiveEventCard.uxml";

        private VisualElement section = null!;
        private LobbyLiveEventsRail events = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            events = new LobbyLiveEventsRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void HideTheSectionWhileNothingIsLive()
        {
            //Act
            events.Show(section);
            events.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void ShowOneCardPerEventOnItsOwnPage()
        {
            //Arrange
            events.Show(section);

            //Act
            events.SetCount(3);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(3, events.Cards.Count);
            Assert.AreEqual(3, Rail().PageCount);
            Assert.AreEqual(0, Rail().CurrentPage);
        }

        [Test]
        public void ReportTheClickedCardByIndex()
        {
            //Arrange
            var clicked = -1;
            events.CardClicked = index => clicked = index;
            events.Show(section);
            events.SetCount(2);

            //Act
            Card(1).Clicked!.Invoke();

            //Assert
            Assert.AreEqual(1, clicked);
        }

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private LobbyLiveEventCardElement Card(int index) =>
            (LobbyLiveEventCardElement)Rail()[index];

        // The live events section of the document, without its stylesheet
        private static VisualElement CreateSection()
        {
            var liveEvents = new VisualElement { name = "LiveEvents" };
            liveEvents.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 1 });
            return liveEvents;
        }
    }
}
