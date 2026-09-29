using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLandingCardElementShould
    {
        private const string TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyLandingCard.uxml";

        private LobbyLandingCardElement card = null!;

        [SetUp]
        public void SetUp()
        {
            card = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TEMPLATE_PATH).InstantiateForElement<LobbyLandingCardElement>();
        }

        [Test]
        public void KeepWhatItIsGivenUntilItsChildrenAttach()
        {
            //Act
            card.Title = "Genesis Plaza";
            card.Creator = "creator";
            card.OnlineCount = 7;
            card.CanJumpIn = false;

            //Assert
            Assert.AreEqual("Genesis Plaza", card.Title);
            Assert.AreEqual("creator", card.Creator);
            Assert.AreEqual(7, card.OnlineCount);
            Assert.IsFalse(card.CanJumpIn);
        }

        [Test]
        public void HideTheOnlineCountWhileItHasNone()
        {
            //Act
            card.HasOnlineCount = false;

            //Assert
            Assert.IsFalse(card.ClassListContains("lobby-landing-card--with-online"));

            //Act
            card.HasOnlineCount = true;

            //Assert
            Assert.IsTrue(card.ClassListContains("lobby-landing-card--with-online"));
        }

        [Test]
        public void MarkTheThumbnailAsLoading()
        {
            //Act
            card.IsLoading = true;

            //Assert
            Assert.IsTrue(card.ClassListContains("lobby-landing-card--loading"));

            //Act
            card.IsLoading = false;

            //Assert
            Assert.IsFalse(card.ClassListContains("lobby-landing-card--loading"));
        }

        [Test]
        public void ReportTheCardAndTheJumpInClicksApart()
        {
            //Arrange
            var clicked = 0;
            var jumpedIn = 0;
            card.Clicked = () => clicked++;
            card.JumpInClicked = () => jumpedIn++;

            //Act
            card.Clicked.Invoke();
            card.JumpInClicked.Invoke();
            card.JumpInClicked.Invoke();

            //Assert
            Assert.AreEqual(1, clicked);
            Assert.AreEqual(2, jumpedIn);
        }
    }
}
