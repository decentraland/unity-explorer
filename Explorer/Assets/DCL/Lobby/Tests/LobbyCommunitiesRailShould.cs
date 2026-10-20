using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyCommunitiesRailShould
    {
        private const string CARD_TEMPLATE_PATH = "Assets/DCL/Lobby/UIToolkit/LobbyCommunityCard.uxml";

        private VisualElement section = null!;
        private LobbyCommunitiesRail communities = null!;

        [SetUp]
        public void SetUp()
        {
            section = CreateSection();
            communities = new LobbyCommunitiesRail(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_TEMPLATE_PATH));
        }

        [Test]
        public void ShowTheBannerInsteadOfTheRailWithoutCommunities()
        {
            //Act
            communities.Show(section);
            communities.SetCount(0);

            //Assert
            Assert.AreEqual(DisplayStyle.Flex, section.style.display.value);
            Assert.AreEqual(DisplayStyle.None, Rail().style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, Empty().style.display.value);
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
        public void KeepTheBannerUpInTheNextSectionWhileStillEmpty()
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
            Assert.AreEqual(DisplayStyle.None, nextSection.Q<LobbyRailElement>().style.display.value);
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

        private LobbyRailElement Rail() =>
            section.Q<LobbyRailElement>();

        private VisualElement Empty() =>
            section.Q("Empty");

        private LobbyCommunityCardElement Card(int index) =>
            (LobbyCommunityCardElement)Rail()[index];

        private static VisualElement CreateSection()
        {
            var communitiesSection = new VisualElement { name = "Communities" };
            communitiesSection.Add(new VisualElement { name = "Empty" });
            communitiesSection.Add(new LobbyRailElement { name = "Rail", CardsPerPage = 3 });
            return communitiesSection;
        }
    }
}
