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

        private readonly List<ReelThumbnailController> reelThumbnailControllers = new ();
        private readonly ReelGalleryPoolManager reelGalleryPoolManager;

        public DateTime DateTimeBucket { get; private set; }

        public MonthGridController(MonthGridView view,
            ReelGalleryPoolManager reelGalleryPool)
        {
            this.view = view;
            this.reelGalleryPoolManager = reelGalleryPool;
        }

        public IReadOnlyList<ReelThumbnailController> Setup(DateTime bucket,
            List<CameraReelResponseCompact> images,
            OptionButtonController? optionsButton,
            Action<CameraReelResponseCompact, Sprite> onThumbnailLoaded,
            Action<CameraReelResponseCompact> onThumbnailClicked)
        {
            this.DateTimeBucket = bucket;
            view.monthText.SetText(bucket.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            List<ReelThumbnailController> newControllers = new();

            for (int i = 0; i < images.Count; i++)
            {
                ReelThumbnailController thumbnailController = reelGalleryPoolManager.GetThumbnailElement(view.gridLayoutGroup);
                thumbnailController.Setup(images[i], optionsButton);
                thumbnailController.ThumbnailLoaded += onThumbnailLoaded;
                thumbnailController.ThumbnailClicked += onThumbnailClicked;
                newControllers.Add(thumbnailController);
            }

            reelThumbnailControllers.AddRange(newControllers);

            return newControllers;
        }

        public void RemoveThumbnail(string reelId)
        {
            for (int i = 0; i < reelThumbnailControllers.Count; i++)
                if (reelThumbnailControllers[i].CameraReelResponse.id == reelId)
                {
                    reelThumbnailControllers[i].Release();
                    reelGalleryPoolManager.ReleaseThumbnailElement(reelThumbnailControllers[i]);
                    reelThumbnailControllers.RemoveAt(i);
                }
        }

        public bool GridIsEmpty() =>
            reelThumbnailControllers.Count == 0;

        public void Release()
        {
            for (int i = 0; i < reelThumbnailControllers.Count; i++)
            {
                reelThumbnailControllers[i].Release();
                reelGalleryPoolManager.ReleaseThumbnailElement(reelThumbnailControllers[i]);
            }
            reelThumbnailControllers.Clear();
        }

        public void Dispose() =>
            reelThumbnailControllers.Clear();
    }
}
