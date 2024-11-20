using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class MonthGridController : IDisposable
    {
        internal readonly MonthGridView view;

        private readonly List<ReelThumbnailController> reelThumbnailViews = new ();
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;

        public DateTime DateTimeBucket { get; private set; }

        public MonthGridController(MonthGridView view,
            ReelGalleryPoolManager reelGalleryPool)
        {
            this.view = view;
            this.reelGalleryPoolManager = reelGalleryPool;
        }

        public List<ReelThumbnailController> Setup(DateTime bucket,
            List<CameraReelResponseCompact> images,
            OptionButtonController optionsButton,
            Action<CameraReelResponseCompact, Sprite> onThumbnailLoaded,
            Action<CameraReelResponseCompact> onThumbnailClicked)
        {
            this.DateTimeBucket = bucket;
            view.monthText.SetText(bucket.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            List<ReelThumbnailController> newViews = new();

            for (int i = 0; i < images.Count; i++)
            {
                ReelThumbnailController thumbnailView = reelGalleryPoolManager.GetThumbnailElement(images[i], view.gridLayoutGroup, optionsButton);
                thumbnailView.ThumbnailLoaded += onThumbnailLoaded;
                thumbnailView.ThumbnailClicked += onThumbnailClicked;
                newViews.Add(thumbnailView);
            }

            reelThumbnailViews.AddRange(newViews);

            return newViews;
        }

        public void PoolGet() =>
            view.gameObject.SetActive(true);

        public void PoolRelease(Transform parent)
        {
            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(false);
        }

        public void RemoveThumbnail(string reelId)
        {
            for (int i = 0; i < reelThumbnailViews.Count; i++)
                if (reelThumbnailViews[i].CameraReelResponse.id == reelId)
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

        public void Dispose()
        {
            reelThumbnailViews.Clear();
        }
    }
}
