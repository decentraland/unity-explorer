using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class MonthGridView : MonoBehaviour
    {
        [SerializeField] private TMP_Text monthText;
        [SerializeField] private GridLayoutGroup gridLayoutGroup;

        private readonly List<ReelThumbnailView> reelThumbnailViews = new ();
        private ReelThumbnailPoolManager reelThumbnailPoolManager;

        public void Setup(DateTime bucket, List<CameraReelResponse> images, ReelThumbnailPoolManager reelThumbnailPool, ICameraReelScreenshotsStorage cameraReelScreenshotsStorage)
        {
            this.reelThumbnailPoolManager = reelThumbnailPool;

            monthText.SetText(bucket.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            for (int i = 0; i < images.Count; i++)
                reelThumbnailViews.Add(reelThumbnailPoolManager.Get(images[i], gridLayoutGroup, cameraReelScreenshotsStorage));
        }

        private void OnDisable()
        {
            for(int i = 0; i < reelThumbnailViews.Count; i++)
                reelThumbnailPoolManager.Release(reelThumbnailViews[i]);
            reelThumbnailViews.Clear();
        }
    }
}
