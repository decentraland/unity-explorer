using DCL.ExplorePanel;
using NUnit.Framework;
using UI.LayoutGroups;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.Navmap.Tests
{
    public class NavmapViewShould
    {
        private const string PREFAB_PATH = "Assets/DCL/Navmap/Assets/Navmap.prefab";

        private GameObject instance = null!;
        private NavmapView view = null!;
        private RectTransform searchBar = null!;
        private RectTransform searchBand = null!;
        private ScrollRect pillsScroll = null!;
        private RectTransform pills = null!;

        [SetUp]
        public void SetUp()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH));
            view = instance.GetComponent<NavmapView>();
            searchBar = (RectTransform)view.SearchBarView.transform;
            searchBand = (RectTransform)searchBar.parent;
            pillsScroll = view.CategoriesStrip.GetComponent<ScrollRect>();
            pills = pillsScroll.content;
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(instance);

        [Test]
        public void GiveTheCloseSlotBackFromTheSearchBarRightEdgeInTheCompactLayout()
        {
            // Arrange
            float leftEdge = LeftEdge(searchBar);
            float width = searchBar.sizeDelta.x;

            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.AreEqual(leftEdge, LeftEdge(searchBar), 0.01f);
            Assert.AreEqual(width - ExploreSectionModalView.CLOSE_BUTTON_SLOT_WIDTH, searchBar.sizeDelta.x, 0.01f);
        }

        [Test]
        public void CentreTheSearchBarOnTheModalHeaderInTheCompactLayout()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            float centreFromTop = searchBand.anchoredPosition.y + searchBar.anchoredPosition.y;
            Assert.AreEqual(-ExploreSectionModalView.HEADER_HEIGHT / 2f, centreFromTop, 0.01f);
        }

        [Test]
        public void WrapTheCategoryPillsAcrossTheStripWidthInTheCompactLayout()
        {
            // Act
            view.SetCompactLayout(true);

            // Assert
            Assert.IsTrue(view.CategoriesStrip.IsWrapping);
            Assert.IsTrue(pills.GetComponent<WrappingLayoutGroup>().enabled);
            Assert.IsFalse(pills.GetComponent<HorizontalLayoutGroup>().enabled);
            Assert.IsFalse(pillsScroll.enabled);
            Assert.IsFalse(view.CategoriesStrip.GetComponent<RectMask2D>().enabled);
            Assert.AreEqual(ContentSizeFitter.FitMode.Unconstrained, pills.GetComponent<ContentSizeFitter>().horizontalFit);
            Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, pills.GetComponent<ContentSizeFitter>().verticalFit);
            Assert.AreEqual(0f, pills.anchorMin.x);
            Assert.AreEqual(1f, pills.anchorMax.x);
            Assert.AreEqual(0f, pills.sizeDelta.x);
        }

        [Test]
        public void RestoreTheAuthoredLayoutWhenNoLongerCompact()
        {
            // Arrange
            Vector2 searchBarSize = searchBar.sizeDelta;
            Vector2 searchBarPosition = searchBar.anchoredPosition;
            Vector2 pillsAnchorMax = pills.anchorMax;
            Vector2 pillsSize = pills.sizeDelta;
            ContentSizeFitter fitter = pills.GetComponent<ContentSizeFitter>();
            ContentSizeFitter.FitMode horizontalFit = fitter.horizontalFit;
            ContentSizeFitter.FitMode verticalFit = fitter.verticalFit;

            // Act
            view.SetCompactLayout(true);
            view.SetCompactLayout(false);

            // Assert
            AssertApproximately(searchBarSize, searchBar.sizeDelta);
            AssertApproximately(searchBarPosition, searchBar.anchoredPosition);
            Assert.IsFalse(view.CategoriesStrip.IsWrapping);
            Assert.IsTrue(pills.GetComponent<HorizontalLayoutGroup>().enabled);
            Assert.IsFalse(pills.GetComponent<WrappingLayoutGroup>().enabled);
            Assert.IsTrue(pillsScroll.enabled);
            Assert.IsTrue(view.CategoriesStrip.GetComponent<RectMask2D>().enabled);
            Assert.AreEqual(horizontalFit, fitter.horizontalFit);
            Assert.AreEqual(verticalFit, fitter.verticalFit);
            AssertApproximately(pillsAnchorMax, pills.anchorMax);
            AssertApproximately(pillsSize, pills.sizeDelta);
        }

        [Test]
        public void KeepTheAuthoredLayoutWhenShownAtHome()
        {
            // Arrange
            Vector2 searchBarSize = searchBar.sizeDelta;

            // Act
            view.SetCompactLayout(false);

            // Assert
            AssertApproximately(searchBarSize, searchBar.sizeDelta);
            Assert.IsFalse(view.CategoriesStrip.IsWrapping);
            Assert.IsTrue(pills.GetComponent<HorizontalLayoutGroup>().enabled);
        }

        private static float LeftEdge(RectTransform rect) =>
            rect.anchoredPosition.x - (rect.sizeDelta.x * rect.pivot.x);

        private static void AssertApproximately(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f);
            Assert.AreEqual(expected.y, actual.y, 0.01f);
        }
    }
}
