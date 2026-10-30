using DCL.UIToolkit.Elements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyCommunitiesRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyCommunityCard.uxml";

        private VisualElement section = null!;
        private LobbyCommunitiesRail communities = null!;
        private GameObject? documentGameObject;
        private PanelSettings? panelSettings;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            communities = new LobbyCommunitiesRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
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
            communities.Show(section);

            //Assert
            Assert.IsTrue(communities.IsLoading);
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Loading().style.display.value);
            Assert.IsTrue(Loading().IsAnimating);
            Assert.AreEqual(DisplayStyle.None, Empty().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
        }

        [Test]
        public void ShowTheBannerInsteadOfTheRailWithoutCommunities()
        {
            //Act
            communities.Show(section);
            communities.SetCount(0);

            //Assert
            Assert.IsFalse(communities.IsLoading);
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Loading().style.display.value);
            Assert.IsFalse(Loading().IsAnimating);
        }

        [Test]
        public void SwapTheBannerForTheCardsOnceTheCommunitiesArrive()
        {
            //Arrange
            communities.Show(section);

            //Act
            communities.SetCount(2);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.None, Empty().style.display.value);
            Assert.AreEqual(2, Rail().childCount);
        }

        [Test]
        public void ShowTheBannerAgainWhenTheLastCommunityGoes()
        {
            //Arrange
            communities.Show(section);
            communities.SetCount(2);

            //Act
            communities.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
        }

        [Test]
        public void KeepTheBannerNotTheSkeletonUpInTheNextSectionWhileStillEmpty()
        {
            //Arrange
            communities.Show(section);
            communities.SetCount(0);
            communities.Hide();
            VisualElement nextSection = CreateSection();

            //Act
            communities.Show(nextSection);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, nextSection.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, nextSection.Q("Empty").style.display.value);
            Assert.AreEqual(DisplayStyle.None, nextSection.Q<SkeletonLoadingElement>("Loading").style.display.value);
            Assert.AreEqual(DisplayStyle.None, nextSection.Q<LobbyRailElement>().style.display.value);
        }

        [Test]
        public void StopTheSkeletonOfAHandedBackSection()
        {
            //Arrange
            communities.Show(section);

            //Act
            communities.Hide();

            //Assert
            Assert.IsFalse(Loading().IsAnimating);
        }

        [Test]
        public void ReportTheClickedCardByIndex()
        {
            //Arrange
            var clicked = -1;
            communities.CardClicked = index => clicked = index;
            communities.Show(section);
            communities.SetCount(2);

            //Act
            Card(0).Clicked!.Invoke();

            //Assert
            Assert.AreEqual(0, clicked);
        }

        [Test]
        public void ReportTheViewAllClickOfItsSection()
        {
            //Arrange
            var clicked = 0;
            communities.ViewAllClicked = () => clicked++;
            Button viewAll = AddViewAll(section);
            AttachToPanel(section);

            //Act
            communities.Show(section);
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

        private LobbyCommunityCardElement Card(int index) =>
            (LobbyCommunityCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var communitiesSection = new VisualElement { name = "Communities" };
            communitiesSection.Add(new SkeletonLoadingElement { name = "Loading", BoneCount = 3 });
            communitiesSection.Add(new VisualElement { name = "Empty" });
            communitiesSection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 3 });
            return communitiesSection;
        }

        private static Button AddViewAll(VisualElement communitiesSection)
        {
            var viewAll = new Button { name = "ViewAll" };
            communitiesSection.Add(viewAll);
            return viewAll;
        }

        // A button hears a submit only through a panel
        private void AttachToPanel(VisualElement element)
        {
            documentGameObject = new GameObject(nameof(LobbyCommunitiesRailShould));
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
