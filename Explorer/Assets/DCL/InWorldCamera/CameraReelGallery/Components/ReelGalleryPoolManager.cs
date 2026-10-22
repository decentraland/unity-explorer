using DCL.InWorldCamera.CameraReelStorageService;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ReelGalleryPoolManager
    {
        private readonly IObjectPool<ReelThumbnailController> reelThumbnailPool;
        private readonly IObjectPool<MonthGridController> reelGridPool;

        private int gridColumns;
        private Vector2 gridCellSize;

        public ReelGalleryPoolManager(
            ReelThumbnailView reelThumbnailPrefab,
            MonthGridView monthViewPrefab,
            GameObject unusedThumbnailPoolObjectParent,
            GameObject unusedGridPoolObjectParent,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService,
            ReelGalleryConfigParams reelGalleryConfigParams,
            int thumbnailDefaultCapacity,
            int thumbnailMaxSize,
            int gridDefaultCapacity,
            int gridMaxSize)
        {
            gridColumns = reelGalleryConfigParams.GridLayoutFixedColumnCount;
            gridCellSize = new Vector2(reelGalleryConfigParams.ThumbnailWidth, reelGalleryConfigParams.ThumbnailHeight);

            reelThumbnailPool = new ObjectPool<ReelThumbnailController>(
                () =>
                {
                    ReelThumbnailView view = Object.Instantiate(reelThumbnailPrefab);
                    return new ReelThumbnailController(view, cameraReelScreenshotsStorageService);
                },
                thumbnail =>
                    thumbnail.PoolGet(),
                thumbnail =>
                    thumbnail.PoolRelease(unusedThumbnailPoolObjectParent.transform),
                thumbnail =>
                {
                    Object.Destroy(thumbnail.view.gameObject);
                    thumbnail.Dispose();
                },
                true,
                thumbnailDefaultCapacity,
                thumbnailMaxSize);

            reelGridPool = new ObjectPool<MonthGridController>(
                () =>
                {
                    MonthGridView view = Object.Instantiate(monthViewPrefab);
                    return new MonthGridController(view, this);
                },
                grid =>
                {
                    grid.view.gridLayoutGroup.constraintCount = gridColumns;
                    grid.view.gridLayoutGroup.cellSize = gridCellSize;
                    grid.view.monthText.gameObject.SetActive(reelGalleryConfigParams.GridShowMonth);
                    grid.view.gameObject.SetActive(true);
                },
                grid =>
                {
                    grid.view.transform.SetParent(unusedGridPoolObjectParent.transform, false);
                    grid.view.gameObject.SetActive(false);
                },
                grid => Object.Destroy(grid.view.gameObject),
                true,
                gridDefaultCapacity,
                gridMaxSize);
        }

        public ReelThumbnailController GetThumbnailElement(GridLayoutGroup parent)
        {
            ReelThumbnailController result = reelThumbnailPool.Get();
            result.view.transform.SetParent(parent.transform, false);
            return result;
        }

        public void ReleaseThumbnailElement(ReelThumbnailController view) =>
            reelThumbnailPool.Release(view);

        public MonthGridController GetGridElement(RectTransform parent)
        {
            MonthGridController result = reelGridPool.Get();
            result.view.transform.SetParent(parent, false);
            return result;
        }

        public void ReleaseGridElement(MonthGridController monthGridView) =>
            reelGridPool.Release(monthGridView);

        /// <summary>
        ///     Lays out the grids taken out from now on; the ones out already keep theirs until they come back.
        /// </summary>
        public void SetGridLayout(int columns, Vector2 cellSize)
        {
            gridColumns = columns;
            gridCellSize = cellSize;
        }
    }
}
