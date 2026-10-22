using DCL.Communities.CommunitiesBrowser;
using DCL.ExplorePanel;
using DCL.UI;
using NUnit.Framework;
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
        private const int COMPACT_COLUMNS = 3;

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

            // Act
            view.SetCompactLayout(true);
            view.SetCompactLayout(false);

            // Assert
            Assert.AreEqual(searchBarPosition.x, searchBar.anchoredPosition.x, 0.01f);
            Assert.AreEqual(searchBarPosition.y, searchBar.anchoredPosition.y, 0.01f);
            Assert.AreEqual(AUTHORED_COLUMNS, Grid("InvitesContainer").constraintCount);
            Assert.AreEqual(AUTHORED_COLUMNS, Grid("RequestsContainer").constraintCount);
            Assert.IsTrue(Find(filteredCommunities, "Card (3)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (9)").activeSelf);
        }

        [Test]
        public void FitTheInvitesAndRequestsGridsToTheCompactColumns()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(COMPACT_COLUMNS, Grid("InvitesContainer").constraintCount);
            Assert.AreEqual(COMPACT_COLUMNS, Grid("RequestsContainer").constraintCount);
        }

        [Test]
        public void HideTheLoadingCardsBeyondTheCompactColumns()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.IsTrue(Find(filteredCommunities, "Card (2)").activeSelf);
            Assert.IsFalse(Find(filteredCommunities, "Card (3)").activeSelf);
            Assert.IsFalse(Find(filteredCommunities, "Card (4)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (2)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (3)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (4)").activeSelf);
            Assert.IsTrue(Find(invitesAndRequests, "Card (7)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (8)").activeSelf);
            Assert.IsFalse(Find(invitesAndRequests, "Card (9)").activeSelf);
        }

        private GridLayoutGroup Grid(string name) =>
            Find(invitesAndRequests, name).GetComponent<GridLayoutGroup>();

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
