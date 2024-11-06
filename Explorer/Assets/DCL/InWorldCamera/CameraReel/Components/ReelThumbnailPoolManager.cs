using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class ReelThumbnailPoolManager
    {
        private readonly IObjectPool<ReelThumbnailView> reelThumbnailPool;
        public ReelThumbnailPoolManager(
            ReelThumbnailView reelThumbnailPrefab,
            GameObject unusedPoolObjectParent,
            int defaultCapacity,
            int maxSize)
        {
            reelThumbnailPool = new ObjectPool<ReelThumbnailView>(
                () => GameObject.Instantiate(reelThumbnailPrefab),
                thumbnail => thumbnail.gameObject.SetActive(true),
                thumbnail =>
                {
                    thumbnail.transform.SetParent(unusedPoolObjectParent.transform, false);
                    thumbnail.gameObject.SetActive(false);
                },
                thumbnail => GameObject.Destroy(thumbnail.gameObject),
                true,
                defaultCapacity,
                maxSize);
        }

        public ReelThumbnailView Get(CameraReelResponse cameraReelResponse, GridLayoutGroup parent, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage)
        {
            ReelThumbnailView result = reelThumbnailPool.Get();
            result.transform.SetParent(parent.transform, false);
            result.Setup(cameraReelResponse, cameraReelScreenshotsStorage);

            return result;
        }

        public void Release(ReelThumbnailView view) =>
            reelThumbnailPool.Release(view);

    }
}
