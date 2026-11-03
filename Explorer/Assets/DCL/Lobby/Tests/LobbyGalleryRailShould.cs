using DCL.UIToolkit.Elements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyGalleryRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyGalleryCard.uxml";

        private VisualElement section = null!;
        private LobbyGalleryRail gallery = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            gallery = new LobbyGalleryRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
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
        public void ShowTheSkeletonUntilTheFirstCount()
        {
            //Act
            gallery.Show(section);

            //Assert
            Assert.IsTrue(gallery.IsLoading);
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Loading().style.display.value);
            Assert.IsTrue(Loading().IsAnimating);
            Assert.AreEqual(DisplayStyle.None, Empty().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
        }

        [Test]
        public void ShowTheBannerInsteadOfTheRailWithoutPhotos()
        {
            //Act
            gallery.Show(section);
            gallery.SetCount(0);

            //Assert
            Assert.IsFalse(gallery.IsLoading);
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Loading().style.display.value);
            Assert.IsFalse(Loading().IsAnimating);
        }

        [Test]
        public void SwapTheBannerForTheCardsOnceThePhotosArrive()
        {
            //Arrange
            gallery.Show(section);

            //Act
            gallery.SetCount(3);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Empty().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Loading().style.display.value);
            Assert.IsFalse(Loading().IsAnimating);
            Assert.AreEqual(3, Rail().childCount);
        }

        [Test]
        public void ShowTheBannerAgainWhenTheLastPhotoGoes()
        {
            //Arrange
            gallery.Show(section);
            gallery.SetCount(3);

            //Act
            gallery.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
            Assert.AreEqual(3, Rail().childCount, "Cards are cloned once and kept for the next fill");
        }

        [Test]
        public void ReportTheClickedCardByIndex()
        {
            //Arrange
            var clicked = -1;
            gallery.CardClicked = index => clicked = index;
            gallery.Show(section);
            gallery.SetCount(2);

            //Act
            Card(1).Clicked!.Invoke();

            //Assert
            Assert.AreEqual(1, clicked);
        }

        [Test]
        public void ReportTheViewAllClickOfItsSection()
        {
            //Arrange
            var clicked = 0;
            gallery.ViewAllClicked = () => clicked++;
            Button viewAll = AddViewAll(section);
            AttachToPanel(section);

            //Act
            gallery.Show(section);
            Submit(viewAll);

            //Assert
            Assert.AreEqual(1, clicked);
        }

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private VisualElement Empty() =>
            section.Q("Empty");

        private SkeletonLoadingElement Loading() =>
            section.Q<SkeletonLoadingElement>("Loading");

        private LobbyGalleryCardElement Card(int index) =>
            (LobbyGalleryCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var gallerySection = new VisualElement { name = "Gallery" };
            gallerySection.Add(new SkeletonLoadingElement { name = "Loading", BoneCount = 3 });
            gallerySection.Add(new VisualElement { name = "Empty" });
            gallerySection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 3 });
            return gallerySection;
        }

        private static Button AddViewAll(VisualElement gallerySection)
        {
            var viewAll = new Button { name = "ViewAll" };
            gallerySection.Add(viewAll);
            return viewAll;
        }

        // A button hears a submit only through a panel
        private void AttachToPanel(VisualElement element)
        {
            documentGameObject = new GameObject(nameof(LobbyGalleryRailShould));
            var document = documentGameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = panelSettings;
            document.rootVisualElement.Add(element);
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
