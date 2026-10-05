using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyFriendsRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyFriendCard.uxml";

        private VisualElement section = null!;
        private LobbyFriendsRail friends = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            friends = new LobbyFriendsRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void HideTheSectionWithoutOnlineFriends()
        {
            //Act
            friends.Show(section);
            friends.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.None, section.style.display.value);
            Assert.AreEqual("0 Online", OnlineCount().text);
        }

        [Test]
        public void CountTheOnlineFriendsInTheHeader()
        {
            //Arrange
            friends.Show(section);

            //Act
            friends.SetCount(2);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual("2 Online", OnlineCount().text);
            Assert.AreEqual(2, Rail().childCount);
        }

        [Test]
        public void KeepThePageWhenTheFriendsChangeWithoutRewinding()
        {
            //Arrange
            friends.Show(section);
            friends.SetCount(9);
            Rail().SnapTo(1);

            //Act
            friends.SetCount(6, rewind: false);

            //Assert
            Assert.AreEqual(1, Rail().CurrentPage);
            Assert.AreEqual("6 Online", OnlineCount().text);
        }

        [Test]
        public void ReportTheClickedCardByIndex()
        {
            //Arrange
            var clicked = -1;
            var joined = -1;
            friends.CardClicked = index => clicked = index;
            friends.CardJoinClicked = index => joined = index;
            friends.Show(section);
            friends.SetCount(2);

            //Act
            Card(0).Clicked!.Invoke();
            Card(1).JoinClicked!.Invoke();

            //Assert
            Assert.AreEqual(0, clicked);
            Assert.AreEqual(1, joined);
        }

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private Label OnlineCount() =>
            section.Q<Label>("OnlineCount");

        private LobbyFriendCardElement Card(int index) =>
            (LobbyFriendCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var friendsSection = new VisualElement { name = "Friends" };
            friendsSection.Add(new Label { name = "OnlineCount", text = "0 Online" });
            friendsSection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 4 });
            return friendsSection;
        }
    }
}
