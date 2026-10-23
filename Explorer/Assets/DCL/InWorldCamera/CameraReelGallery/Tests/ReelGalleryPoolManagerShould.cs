using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.CameraReelGallery.Tests
{
    public class ReelGalleryPoolManagerShould
    {
        private const string THUMBNAIL_PREFAB_PATH = "Assets/DCL/InWorldCamera/CameraReelGallery/Assets/ThumbnailElement.prefab";
        private const string MONTH_GRID_PREFAB_PATH = "Assets/DCL/InWorldCamera/CameraReelGallery/Assets/MonthGrid.prefab";

        private static readonly ReelGalleryConfigParams AUTHORED = new (gridLayoutFixedColumnCount: 6, thumbnailHeight: 230, thumbnailWidth: 306, gridShowMonth: true, groupByMonth: true);
        private static readonly Vector2 COMPACT_CELL = new (292f, 219f);

        private GameObject unusedThumbnails = null!;
        private GameObject unusedGrids = null!;
        private RectTransform host = null!;
        private ReelGalleryPoolManager pool = null!;

        [SetUp]
        public void SetUp()
        {
            unusedThumbnails = new GameObject("UnusedThumbnails");
            unusedGrids = new GameObject("UnusedGrids");
            host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>();

            pool = new ReelGalleryPoolManager(
                AssetDatabase.LoadAssetAtPath<GameObject>(THUMBNAIL_PREFAB_PATH).GetComponent<ReelThumbnailView>(),
                AssetDatabase.LoadAssetAtPath<GameObject>(MONTH_GRID_PREFAB_PATH).GetComponent<MonthGridView>(),
                unusedThumbnails,
                unusedGrids,
                Substitute.For<ICameraReelScreenshotsStorage>(),
                AUTHORED,
                thumbnailDefaultCapacity: 1,
                thumbnailMaxSize: 1,
                gridDefaultCapacity: 1,
                gridMaxSize: 2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host.gameObject);
            Object.DestroyImmediate(unusedThumbnails);
            Object.DestroyImmediate(unusedGrids);
        }

        [Test]
        public void LayOutTheGridsItHandsOutAsAuthored()
        {
            // Act
            MonthGridController grid = pool.GetGridElement(host);

            // Assert
            Assert.AreEqual(AUTHORED.GridLayoutFixedColumnCount, grid.view.gridLayoutGroup.constraintCount);
            Assert.AreEqual(new Vector2(AUTHORED.ThumbnailWidth, AUTHORED.ThumbnailHeight), grid.view.gridLayoutGroup.cellSize);
        }

        [Test]
        public void LayOutTheGridsItHandsOutAsSet()
        {
            // Act
            pool.SetGridLayout(5, COMPACT_CELL);
            MonthGridController grid = pool.GetGridElement(host);

            // Assert
            Assert.AreEqual(5, grid.view.gridLayoutGroup.constraintCount);
            Assert.AreEqual(COMPACT_CELL, grid.view.gridLayoutGroup.cellSize);
        }

        [Test]
        public void LayOutAGridAgainWhenItComesBackOut()
        {
            // Arrange
            MonthGridController grid = pool.GetGridElement(host);
            pool.ReleaseGridElement(grid);

            // Act
            pool.SetGridLayout(5, COMPACT_CELL);
            MonthGridController again = pool.GetGridElement(host);

            // Assert
            Assert.AreSame(grid, again);
            Assert.AreEqual(5, again.view.gridLayoutGroup.constraintCount);
            Assert.AreEqual(COMPACT_CELL, again.view.gridLayoutGroup.cellSize);
        }
    }
}
