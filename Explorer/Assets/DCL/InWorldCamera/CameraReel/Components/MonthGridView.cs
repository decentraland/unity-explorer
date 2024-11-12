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
        private ReelGalleryPoolManager reelGalleryPoolManager;

        public List<ReelThumbnailView> Setup(
            DateTime bucket,
            List<CameraReelResponse> images,
            ReelGalleryPoolManager reelGalleryPool,
            ICameraReelScreenshotsStorage cameraReelScreenshotsStorage,
            OptionButtonView optionsButton,
            Action<CameraReelResponse, Sprite> onThumbnailLoaded,
            Action<CameraReelResponse> onThumbnailClicked)
        {
            this.reelGalleryPoolManager = reelGalleryPool;

            monthText.SetText(bucket.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            List<ReelThumbnailView> newViews = new();

            for (int i = 0; i < images.Count; i++)
            {
                ReelThumbnailView thumbnailView = reelGalleryPoolManager.GetThumbnailElement(images[i], gridLayoutGroup, cameraReelScreenshotsStorage, optionsButton);
                thumbnailView.OnThumbnailLoaded += onThumbnailLoaded;
                thumbnailView.OnThumbnailClicked += onThumbnailClicked;
                newViews.Add(thumbnailView);
            }

            reelThumbnailViews.AddRange(newViews);

            return newViews;
        }

        public void RemoveThumbnail(CameraReelResponse cameraReelResponse)
        {
            for (int i = 0; i < reelThumbnailViews.Count; i++)
                if (reelThumbnailViews[i].cameraReelResponse == cameraReelResponse)
                {
                    reelThumbnailViews[i].Release();
                    reelGalleryPoolManager.ReleaseThumbnailElement(reelThumbnailViews[i]);
                    reelThumbnailViews.RemoveAt(i);
                }
        }

        public bool GridIsEmpty() =>
            reelThumbnailViews.Count == 0;

        public void Release()
        {
            for (int i = 0; i < reelThumbnailViews.Count; i++)
            {
                reelThumbnailViews[i].Release();
                reelGalleryPoolManager.ReleaseThumbnailElement(reelThumbnailViews[i]);
            }
            reelThumbnailViews.Clear();
        }

    }
}
