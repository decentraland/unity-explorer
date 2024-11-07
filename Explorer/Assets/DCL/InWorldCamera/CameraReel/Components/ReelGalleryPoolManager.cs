using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class ReelGalleryPoolManager
    {
        private readonly IObjectPool<ReelThumbnailView> reelThumbnailPool;
        private readonly IObjectPool<MonthGridView> reelGridPool;
        public ReelGalleryPoolManager(
            ReelThumbnailView reelThumbnailPrefab,
            MonthGridView monthViewPrefab,
            GameObject unusedThumbnailPoolObjectParent,
            GameObject unusedGridPoolObjectParent,
            int thumbnailDefaultCapacity,
            int thumbnailMaxSize,
            int gridDefaultCapacity,
            int gridMaxSize)
        {
            reelThumbnailPool = new ObjectPool<ReelThumbnailView>(
                () => GameObject.Instantiate(reelThumbnailPrefab),
                thumbnail => thumbnail.gameObject.SetActive(true),
                thumbnail =>
                {
                    thumbnail.transform.SetParent(unusedThumbnailPoolObjectParent.transform, false);
                    thumbnail.gameObject.SetActive(false);
                },
                thumbnail => GameObject.Destroy(thumbnail.gameObject),
                true,
                thumbnailDefaultCapacity,
                thumbnailMaxSize);

            reelGridPool = new ObjectPool<MonthGridView>(
                () => GameObject.Instantiate(monthViewPrefab),
                grid => grid.gameObject.SetActive(true),
                grid =>
                {
                    grid.transform.SetParent(unusedGridPoolObjectParent.transform, false);
                    grid.gameObject.SetActive(false);
                },
                grid => GameObject.Destroy(grid.gameObject),
                true,
                gridDefaultCapacity,
                gridMaxSize);
        }

        public ReelThumbnailView GetThumbnailElement(CameraReelResponse cameraReelResponse, GridLayoutGroup parent, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage, OptionButtonView optionsButton)
        {
            ReelThumbnailView result = reelThumbnailPool.Get();
            result.transform.SetParent(parent.transform, false);
            result.Setup(cameraReelResponse, cameraReelScreenshotsStorage, optionsButton);

            return result;
        }

        public void ReleaseThumbnailElement(ReelThumbnailView view) =>
            reelThumbnailPool.Release(view);

        public MonthGridView GetGridElement(RectTransform parent)
        {
            MonthGridView result = reelGridPool.Get();
            result.transform.SetParent(parent, false);
            return result;
        }

        public void ReleaseGridElement(MonthGridView monthGridView) =>
            reelGridPool.Release(monthGridView);
    }
}
