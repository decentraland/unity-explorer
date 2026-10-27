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
        private int columns;
        private Vector2 cell;

        /// <summary>
        ///     Every slot ever created; the ones past the current column count stay dormant until a wider layout comes back.
        /// </summary>
        internal IReadOnlyList<ReelThumbnailView> slots => thumbnails;

        public bool HasLayout(in ReelGalleryConfigParams config) =>
            columns == config.GridLayoutFixedColumnCount && cell == new Vector2(config.ThumbnailWidth, config.ThumbnailHeight);

        /// <summary>
        ///     The column count and cell size come from the host's config, so the slots cannot be authored in the prefab.
        ///     Slots are kept across layouts: the row is pooled, and a shown slot is never destroyed.
        /// </summary>
        public void Initialize(ReelThumbnailView thumbnailPrefab, in ReelGalleryConfigParams config, CameraReelOptionButtonController? optionButton, Action<int> clicked)
        {
            columns = config.GridLayoutFixedColumnCount;
            cell = new Vector2(config.ThumbnailWidth, config.ThumbnailHeight);

            if (thumbnails.Length < columns)
            {
                int existing = thumbnails.Length;
                Array.Resize(ref thumbnails, columns);

                for (int i = existing; i < columns; i++)
                {
                    ReelThumbnailView slot = Instantiate(thumbnailPrefab, transform, false);
                    var rect = (RectTransform)slot.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    slot.Initialize(optionButton, clicked, cell);
                    thumbnails[i] = slot;
                }
            }

            for (var i = 0; i < thumbnails.Length; i++)
            {
                ReelThumbnailView slot = thumbnails[i];
                slot.Unbind();
                slot.gameObject.SetActive(false);

                if (i >= columns) continue;

                slot.Resize(cell);

                // The prefab pivot stays centered so the hover scale grows around the thumbnail.
                ((RectTransform)slot.transform).anchoredPosition = new Vector2((i * (cell.x + ReelGalleryRow.ROW_SPACING)) + (cell.x / 2f), -cell.y / 2f);
            }

            ((RectTransform)transform).sizeDelta = new Vector2((columns * cell.x) + ((columns - 1) * ReelGalleryRow.ROW_SPACING), cell.y);
        }

        public void Bind(IReadOnlyList<CameraReelResponseCompact> reels, int startIndex, int count, ISpriteCache thumbnailCache)
        {
            for (var i = 0; i < columns; i++)
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
