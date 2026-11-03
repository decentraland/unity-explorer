using DCL.UIToolkit.Elements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyFriendCardElementShould
    {
        private const string TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyFriendCard.uxml";

        private LobbyFriendCardElement card = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            card = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TEMPLATE_PATH).InstantiateForElement<LobbyFriendCardElement>();
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
        public void SweepTheSkeletonOverThePictureWhileLoading()
        {
            //Arrange
            card.IsLoading = true;

            //Act
            AttachToPanel();

            //Assert
            SkeletonLoadingElement skeleton = card.Q<VisualElement>("Picture").Q<SkeletonLoadingElement>("Skeleton");
            Assert.IsTrue(card.ClassListContains("lobby-friend-card--loading"));
            Assert.IsTrue(skeleton.ClassListContains("lobby-thumbnail-skeleton--shown"));
            Assert.IsTrue(skeleton.IsAnimating);

            //Act
            card.IsLoading = false;

            //Assert
            Assert.IsFalse(card.ClassListContains("lobby-friend-card--loading"));
            Assert.IsFalse(skeleton.ClassListContains("lobby-thumbnail-skeleton--shown"));
            Assert.IsFalse(skeleton.IsAnimating);
        }

        private void AttachToPanel()
        {
            documentGameObject = new GameObject(nameof(LobbyFriendCardElementShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(card);
        }
    }
}
