using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyConnectedFriendsElementShould
    {
        private const string SHOWN = "lobby-connected-friends--shown";
        private const string WITH_OVERFLOW = "lobby-connected-friends--with-overflow";
        private const string PICTURE_LOADING = "lobby-connected-friends__picture--loading";

        private LobbyConnectedFriendsElement friends = null!;

        [SetUp]
        public void SetUp()
        {
            friends = new LobbyConnectedFriendsElement();
        }

        [Test]
        public void HideWithoutFriends()
        {
            //Act
            friends.Count = 0;

            //Assert
            Assert.IsFalse(friends.ClassListContains(SHOWN));
            Assert.IsFalse(friends.ClassListContains(WITH_OVERFLOW));

            for (var i = 0; i < LobbyConnectedFriendsElement.MAX_SLOTS; i++)
                Assert.AreEqual(DisplayStyle.None, friends.Slot(i).style.display.value);
        }

        [Test]
        public void ShowOneSlotPerFriendUpToThree()
        {
            //Act
            friends.Count = 2;

            //Assert
            Assert.IsTrue(friends.ClassListContains(SHOWN));
            Assert.IsFalse(friends.ClassListContains(WITH_OVERFLOW));
            Assert.AreEqual(DisplayStyle.Flex, friends.Slot(0).style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, friends.Slot(1).style.display.value);
            Assert.AreEqual(DisplayStyle.None, friends.Slot(2).style.display.value);
            Assert.AreEqual(string.Empty, friends.Q<Label>().text);
        }

        [Test]
        public void CountTheFriendsPastTheSlots()
        {
            //Act
            friends.Count = 5;

            //Assert
            Assert.IsTrue(friends.ClassListContains(SHOWN));
            Assert.IsTrue(friends.ClassListContains(WITH_OVERFLOW));

            for (var i = 0; i < LobbyConnectedFriendsElement.MAX_SLOTS; i++)
                Assert.AreEqual(DisplayStyle.Flex, friends.Slot(i).style.display.value);

            Assert.AreEqual("+2", friends.Q<Label>().text);
        }

        [Test]
        public void ApplyThePictureToItsSlot()
        {
            //Act
            friends.SetPicture(1, null, Color.red, loading: true);

            //Assert
            Assert.AreEqual(Color.red, friends.Slot(1).style.backgroundColor.value);
            Assert.IsTrue(friends.Slot(1).ClassListContains(PICTURE_LOADING));
            Assert.IsFalse(friends.Slot(0).ClassListContains(PICTURE_LOADING));

            //Act
            friends.SetPicture(1, null, Color.blue, loading: false);

            //Assert
            Assert.AreEqual(Color.blue, friends.Slot(1).style.backgroundColor.value);
            Assert.IsFalse(friends.Slot(1).ClassListContains(PICTURE_LOADING));
        }

        [Test]
        public void LetTheSlotsTakeTheHoverButNotTheRow()
        {
            //Assert
            Assert.AreEqual(PickingMode.Ignore, friends.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, friends.Q<Label>().pickingMode);

            for (var i = 0; i < LobbyConnectedFriendsElement.MAX_SLOTS; i++)
                Assert.AreEqual(PickingMode.Position, friends.Slot(i).pickingMode);
        }
    }
}
