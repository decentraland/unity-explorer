using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ReelGalleryPoolManager
    {
        private readonly IObjectPool<ReelThumbnailController> reelThumbnailPool;
        private readonly IObjectPool<MonthGridController> reelGridPool;
        public ReelGalleryPoolManager(
            ReelThumbnailView reelThumbnailPrefab,
            MonthGridView monthViewPrefab,
            GameObject unusedThumbnailPoolObjectParent,
            GameObject unusedGridPoolObjectParent,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorageService,
            int thumbnailDefaultCapacity,
            int thumbnailMaxSize,
            int gridDefaultCapacity,
            int gridMaxSize)
        {
            reelThumbnailPool = new ObjectPool<ReelThumbnailController>(
                () =>
                {
                    ReelThumbnailView view = GameObject.Instantiate(reelThumbnailPrefab);
                    return new ReelThumbnailController(view, cameraReelScreenshotsStorageService);
                },
                thumbnail =>
                    thumbnail.PoolGet(),
                thumbnail =>
                    thumbnail.PoolRelease(unusedThumbnailPoolObjectParent.transform),
                thumbnail =>
                {
                    GameObject.Destroy(thumbnail.view.gameObject);
                    thumbnail.Dispose();
                },
                true,
                thumbnailDefaultCapacity,
                thumbnailMaxSize);

            reelGridPool = new ObjectPool<MonthGridController>(
                () =>
                {
                    MonthGridView view = GameObject.Instantiate(monthViewPrefab);
                    return new MonthGridController(view, this);
                },
                grid => grid.view.gameObject.SetActive(true),
                grid =>
                {
                    grid.view.transform.SetParent(unusedGridPoolObjectParent.transform, false);
                    grid.view.gameObject.SetActive(false);
                },
                grid => GameObject.Destroy(grid.view.gameObject),
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
    }
}
