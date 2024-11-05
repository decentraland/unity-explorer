using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;
using UnityEngine.Pool;

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
                    thumbnail.transform.parent = unusedPoolObjectParent.transform;
                    thumbnail.gameObject.SetActive(false);
                },
                thumbnail => GameObject.Destroy(thumbnail.gameObject),
                true,
                defaultCapacity,
                maxSize);
        }

        public ReelThumbnailView Get(CameraReelResponse cameraReelResponse, MonthGridView parent)
        {
            ReelThumbnailView result = reelThumbnailPool.Get();
            result.transform.parent = parent.transform;
            result.Setup(cameraReelResponse);

            return result;
        }

        public void Release(ReelThumbnailView view) =>
            reelThumbnailPool.Release(view);

    }
}
