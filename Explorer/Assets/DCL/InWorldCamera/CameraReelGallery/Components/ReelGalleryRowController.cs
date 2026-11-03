using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     Owns the thumbnail controllers of one pooled row and binds a slice of the reel list to them.
    /// </summary>
    public class ReelGalleryRowController : IDisposable
    {
        private readonly ReelGalleryRowView view;
        private readonly CameraReelOptionButtonController? optionButton;
        private readonly Action<int> clicked;
        private readonly List<ReelThumbnailController> thumbnailControllers = new ();

        internal IReadOnlyList<ReelThumbnailController> thumbnails => thumbnailControllers;

        public ReelGalleryRowController(ReelGalleryRowView view, CameraReelOptionButtonController? optionButton, Action<int> clicked)
        {
            this.view = view;
            this.optionButton = optionButton;
            this.clicked = clicked;

            // The pool deactivates a recycled row, so its in-flight loads are cancelled here.
            view.Disabled += Unbind;
        }

        public void Dispose()
        {
            view.Disabled -= Unbind;

            foreach (ReelThumbnailController thumbnail in thumbnailControllers)
                thumbnail.Dispose();

            thumbnailControllers.Clear();
        }

        public void EnsureLayout(ReelThumbnailView thumbnailPrefab, in ReelGalleryConfigParams config)
        {
            if (view.HasLayout(in config)) return;

            Unbind();
            view.Initialize(thumbnailPrefab, in config);

            for (int i = thumbnailControllers.Count; i < view.slots.Count; i++)
                thumbnailControllers.Add(new ReelThumbnailController(view.slots[i], optionButton, clicked));
        }

        public void Bind(IReadOnlyList<CameraReelResponseCompact> reels, int startIndex, int count, ISpriteCache thumbnailCache)
        {
            for (var i = 0; i < thumbnailControllers.Count; i++)
                if (i < count)
                    thumbnailControllers[i].Bind(reels[startIndex + i], startIndex + i, thumbnailCache);
                else
                {
                    thumbnailControllers[i].Unbind();
                    view.slots[i].gameObject.SetActive(false);
                }
        }

        public void Unbind()
        {
            foreach (ReelThumbnailController thumbnail in thumbnailControllers)
                thumbnail.Unbind();
        }
    }
}
