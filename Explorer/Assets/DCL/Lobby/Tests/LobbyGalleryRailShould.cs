using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyGalleryRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyGalleryCard.uxml";

        private VisualElement section = null!;
        private LobbyGalleryRail gallery = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            gallery = new LobbyGalleryRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void ShowTheBannerInsteadOfTheRailWithoutPhotos()
        {
            //Act
            gallery.Show(section);
            gallery.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
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

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private VisualElement Empty() =>
            section.Q("Empty");

        private LobbyGalleryCardElement Card(int index) =>
            (LobbyGalleryCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var gallerySection = new VisualElement { name = "Gallery" };
            gallerySection.Add(new VisualElement { name = "Empty" });
            gallerySection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 3 });
            return gallerySection;
        }
    }
}
