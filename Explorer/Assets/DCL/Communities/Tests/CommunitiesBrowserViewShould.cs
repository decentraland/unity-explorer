using DCL.Communities.CommunitiesBrowser;
using DCL.ExplorePanel;
using DCL.UI;
using NUnit.Framework;
using SuperScrollView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.Communities.Tests
{
    public class CommunitiesBrowserViewShould
    {
        private const string PREFAB_PATH = "Assets/DCL/Communities/CommunitiesBrowser/Prefabs/CommunitiesBrowser.prefab";
        private const int AUTHORED_COLUMNS = 5;
        private const int COMPACT_COLUMNS = 4;
        private const float AUTHORED_CARD_WIDTH = 280f;
        private const float COMPACT_CARD_WIDTH = 260f;
        private const float CARD_SPACING = 15f;

        private GameObject instance = null!;
        private CommunitiesBrowserView view = null!;
        private RectTransform searchBar = null!;
        private Transform filteredCommunities = null!;
        private Transform invitesAndRequests = null!;

        [SetUp]
        public void SetUp()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH));
            view = instance.GetComponent<CommunitiesBrowserView>();
            searchBar = (RectTransform)instance.GetComponentInChildren<SearchBarView>(true).transform;
            filteredCommunities = view.RightSectionView.FilteredCommunitiesView.transform;
            invitesAndRequests = Find(instance.transform, "InvitesAndRequestsSection").transform;

            // The results grid takes layout changes only once initialised, as the presenter does before any host attaches
            view.RightSectionView.FilteredCommunitiesView.InitializeResultsGrid();
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(instance);

        [Test]
        public void GiveTheCloseSlotBackFromTheSearchBarInTheCompactLayout()
        {
            // Arrange
            float rightEdge = RightEdge(searchBar);

            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(rightEdge - ExploreSectionModalView.CLOSE_BUTTON_SLOT_WIDTH, RightEdge(searchBar), 0.01f);
        }

        [Test]
        public void ShiftTheSearchBarOnceHoweverOftenTheCompactLayoutIsSet()
        {
            // Arrange
            float rightEdge = RightEdge(searchBar);

            // Act
            view.SetCompactLayout(true);
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(rightEdge - ExploreSectionModalView.CLOSE_BUTTON_SLOT_WIDTH, RightEdge(searchBar), 0.01f);
        }

        [Test]
        public void RestoreTheAuthoredLayoutWhenNoLongerCompact()
        {
            // Arrange
            Vector2 searchBarPosition = searchBar.anchoredPosition;
            Vector2 lastResultsCardPosition = Card(filteredCommunities, "Card (4)").anchoredPosition;
            Vector2 lastInvitesCardPosition = Card(invitesAndRequests, "Card (9)").anchoredPosition;

            // Act
            view.SetCompactLayout(true);
            view.SetCompactLayout(false);

            // Assert
            Assert.AreEqual(searchBarPosition.x, searchBar.anchoredPosition.x, 0.01f);
            Assert.AreEqual(searchBarPosition.y, searchBar.anchoredPosition.y, 0.01f);
            Assert.AreEqual(AUTHORED_COLUMNS, Grid("InvitesContainer").constraintCount);
            Assert.AreEqual(AUTHORED_COLUMNS, Grid("RequestsContainer").constraintCount);
            Assert.AreEqual(AUTHORED_CARD_WIDTH, Grid("InvitesContainer").cellSize.x, 0.01f);
            Assert.AreEqual(AUTHORED_CARD_WIDTH, ResultsGrid().ItemSize.x, 0.01f);
            Assert.IsTrue(Find(filteredCommunities, "Card (4)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (9)").activeSelf);
            Assert.AreEqual(AUTHORED_CARD_WIDTH, Card(filteredCommunities, "Card (4)").sizeDelta.x, 0.01f);
            Assert.AreEqual(lastResultsCardPosition, Card(filteredCommunities, "Card (4)").anchoredPosition);
            Assert.AreEqual(lastInvitesCardPosition, Card(invitesAndRequests, "Card (9)").anchoredPosition);
        }

        [Test]
        public void FitTheInvitesAndRequestsGridsToTheCompactColumns()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(COMPACT_COLUMNS, Grid("InvitesContainer").constraintCount);
            Assert.AreEqual(COMPACT_COLUMNS, Grid("RequestsContainer").constraintCount);
            Assert.AreEqual(COMPACT_CARD_WIDTH, Grid("InvitesContainer").cellSize.x, 0.01f);
            Assert.AreEqual(COMPACT_CARD_WIDTH, Grid("RequestsContainer").cellSize.x, 0.01f);
        }

        [Test]
        public void ShrinkTheResultCardsToTheCompactWidth()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(COMPACT_CARD_WIDTH, ResultsGrid().ItemSize.x, 0.01f);
        }

        [Test]
        public void HideTheLoadingCardsBeyondTheCompactColumns()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.IsTrue(Find(filteredCommunities, "Card (3)").activeSelf);
            Assert.IsFalse(Find(filteredCommunities, "Card (4)").activeSelf);
            Assert.IsTrue(Find(filteredCommunities, "Card (8)").activeSelf);
            Assert.IsFalse(Find(filteredCommunities, "Card (9)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (3)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (4)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (8)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (9)").activeSelf);
        }

        [Test]
        public void CloseUpTheLoadingCardsToTheCompactWidth()
        {
            // Arrange
            float firstResultsCardLeftEdge = LeftEdge(Card(filteredCommunities, "Card"));
            float firstInvitesCardLeftEdge = LeftEdge(Card(invitesAndRequests, "Card"));

            // Act
            view.SetCompactLayout(true);

            // Assert
            AssertCompactRow(filteredCommunities, firstResultsCardLeftEdge);
            AssertCompactRow(invitesAndRequests, firstInvitesCardLeftEdge);
        }

        private static void AssertCompactRow(Transform root, float firstCardLeftEdge)
        {
            Assert.AreEqual(COMPACT_CARD_WIDTH, Card(root, "Card").sizeDelta.x, 0.01f);
            Assert.AreEqual(firstCardLeftEdge, LeftEdge(Card(root, "Card")), 0.01f);
            Assert.AreEqual(firstCardLeftEdge + (COMPACT_CARD_WIDTH + CARD_SPACING), LeftEdge(Card(root, "Card (1)")), 0.01f);
            Assert.AreEqual(firstCardLeftEdge + (3 * (COMPACT_CARD_WIDTH + CARD_SPACING)), LeftEdge(Card(root, "Card (3)")), 0.01f);
            Assert.AreEqual(firstCardLeftEdge + (3 * (COMPACT_CARD_WIDTH + CARD_SPACING)), LeftEdge(Card(root, "Card (8)")), 0.01f);
        }

        private LoopGridView ResultsGrid() =>
            filteredCommunities.GetComponentInChildren<LoopGridView>(true);

        private GridLayoutGroup Grid(string name) =>
            Find(invitesAndRequests, name).GetComponent<GridLayoutGroup>();

        private static RectTransform Card(Transform root, string name) =>
            (RectTransform)Find(root, name).transform;

        private static float LeftEdge(RectTransform rect) =>
            rect.anchoredPosition.x - (rect.sizeDelta.x * rect.pivot.x);

        private static float RightEdge(RectTransform rect) =>
            rect.anchoredPosition.x + (rect.sizeDelta.x * (1f - rect.pivot.x));

        // Inactive objects included, which Transform.Find does not do across depths
        private static GameObject Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.gameObject;

            throw new AssertionException($"{name} not found under {root.name}");
        }
    }
}
