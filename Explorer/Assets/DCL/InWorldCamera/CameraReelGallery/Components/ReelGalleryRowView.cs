using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     A pooled LoopListView2 item holding one grid row of thumbnail slots.
    /// </summary>
    public class ReelGalleryRowView : MonoBehaviour
    {
        private ReelThumbnailView[] thumbnails = Array.Empty<ReelThumbnailView>();

        public bool IsInitialized => thumbnails.Length > 0;

        internal IReadOnlyList<ReelThumbnailView> slots => thumbnails;

        /// <summary>
        ///     Runs once per pooled row: the column count and cell size come from the host's config,
        ///     so the slots cannot be authored in the prefab.
        /// </summary>
        public void Initialize(ReelThumbnailView thumbnailPrefab, in ReelGalleryConfigParams config, CameraReelOptionButtonController? optionButton, Action<int> clicked)
        {
            int columns = config.GridLayoutFixedColumnCount;
            var cell = new Vector2(config.ThumbnailWidth, config.ThumbnailHeight);
            thumbnails = new ReelThumbnailView[columns];

            for (var i = 0; i < columns; i++)
            {
                ReelThumbnailView slot = Instantiate(thumbnailPrefab, transform, false);
                var rect = (RectTransform)slot.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);

                // The prefab pivot stays centered so the hover scale grows around the thumbnail
                rect.anchoredPosition = new Vector2((i * (cell.x + ReelGalleryRow.ROW_SPACING)) + (cell.x / 2f), -cell.y / 2f);
                slot.Initialize(optionButton, clicked, cell);
                slot.gameObject.SetActive(false);
                thumbnails[i] = slot;
            }

            ((RectTransform)transform).sizeDelta = new Vector2((columns * cell.x) + ((columns - 1) * ReelGalleryRow.ROW_SPACING), cell.y);
        }

        public void Bind(IReadOnlyList<CameraReelResponseCompact> reels, int startIndex, int count, ISpriteCache thumbnailCache)
        {
            for (var i = 0; i < thumbnails.Length; i++)
                if (i < count)
                    thumbnails[i].Bind(reels[startIndex + i], startIndex + i, thumbnailCache);
                else
                {
                    thumbnails[i].Unbind();
                    thumbnails[i].gameObject.SetActive(false);
                }
        }
    }
}
