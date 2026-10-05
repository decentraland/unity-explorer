using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLandingCardElementShould
    {
        private const string TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyLandingCard.uxml";

        private LobbyLandingCardElement card = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            card = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TEMPLATE_PATH).InstantiateForElement<LobbyLandingCardElement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (documentGameObject != null)
                Object.DestroyImmediate(documentGameObject);

            if (panelSettings != null)
                Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void ApplyWhatItWasGivenOnceItsChildrenAttach()
        {
            //Arrange
            card.Title = "Genesis Plaza";
            card.Creator = "creator";
            card.OnlineCount = 7;
            card.CanJumpIn = false;

            //Act
            AttachToPanel();

            //Assert
            Assert.AreEqual("Genesis Plaza", card.Q<Label>("Title").text);
            Assert.AreEqual("creator", card.Q<Label>("Creator").text);
            Assert.AreEqual("7", card.Q<Label>("OnlineCount").text);
            Assert.IsFalse(card.Q<Button>("JumpIn").enabledSelf);
        }

        [Test]
        public void ApplyWhatItIsGivenWhileAttached()
        {
            //Arrange
            AttachToPanel();

            //Act
            card.Title = "Genesis Plaza";
            card.CanJumpIn = true;

            //Assert
            Assert.AreEqual("Genesis Plaza", card.Q<Label>("Title").text);
            Assert.IsTrue(card.Q<Button>("JumpIn").enabledSelf);
        }

        [Test]
        public void ShowTheOnlineCountOnlyWhileSomebodyIsThere()
        {
            //Act
            card.OnlineCount = 0;

            //Assert
            Assert.IsFalse(card.ClassListContains("lobby-landing-card--with-online"));

            //Act
            card.OnlineCount = 3;

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
        public void MarkItselfStaticWhileItCannotOpen()
        {
            //Act
            card.CanOpen = false;

            //Assert
            Assert.IsTrue(card.ClassListContains("lobby-landing-card--static"));
            Assert.IsFalse(card.ClassListContains(VisualElementsExtensions.INTERACTABLE_CLASS));

            //Act
            card.CanOpen = true;

            //Assert
            Assert.IsFalse(card.ClassListContains("lobby-landing-card--static"));
            Assert.IsTrue(card.ClassListContains(VisualElementsExtensions.INTERACTABLE_CLASS));
        }

        [Test]
        public void ReportTheJumpInClickThroughItsButton()
        {
            //Arrange
            AttachToPanel();
            var clicked = 0;
            var jumpedIn = 0;
            card.Clicked = () => clicked++;
            card.JumpInClicked = () => jumpedIn++;

            //Act
            Submit(card.Q<Button>("JumpIn"));

            //Assert
            Assert.AreEqual(1, jumpedIn);
            Assert.AreEqual(0, clicked, "The Jump in button is not a click on the card");
        }

        private void AttachToPanel()
        {
            documentGameObject = new GameObject(nameof(LobbyLandingCardElementShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(card);
        }

        // A submit is reported as a click like a keyboard press; unlike a pointer click it needs no laid-out panel
        private static void Submit(Button button)
        {
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }
    }
}
