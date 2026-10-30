using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    internal enum ReelGalleryRowKind : byte
    {
        Padding,
        MonthHeader,
        Thumbnails,
    }

    /// <summary>
    ///     One LoopListView2 item of the gallery: a padding strip, a month header or one grid row of thumbnails.
    /// </summary>
    internal readonly struct ReelGalleryRow
    {
        // Spacings of the GridLayoutGroup and month layout the list replaced, kept for visual parity.
        public const float ROW_SPACING = 4f;
        public const float HEADER_SPACING = 20f;

        public readonly ReelGalleryRowKind Kind;
        public readonly DateTime Month;
        public readonly int StartIndex;
        public readonly int Count;
        public readonly float Size;
        public readonly float SpacingAfter;

        private ReelGalleryRow(ReelGalleryRowKind kind, DateTime month, int startIndex, int count, float size, float spacingAfter)
        {
            Kind = kind;
            Month = month;
            StartIndex = startIndex;
            Count = count;
            Size = size;
            SpacingAfter = spacingAfter;
        }

        public static ReelGalleryRow Padding(float size) =>
            new (ReelGalleryRowKind.Padding, default(DateTime), 0, 0, size, 0f);

        public static ReelGalleryRow MonthHeader(DateTime month) =>
            new (ReelGalleryRowKind.MonthHeader, month, 0, 0, 0f, HEADER_SPACING);

        public static ReelGalleryRow Thumbnails(int startIndex, int count, float spacingAfter) =>
            new (ReelGalleryRowKind.Thumbnails, default(DateTime), startIndex, count, 0f, spacingAfter);

        /// <summary>
        ///     Consecutive reels of the same month form one section, so <see cref="StartIndex" /> plus the slot index
        ///     is the index into the reel list.
        /// </summary>
        public static void Build(List<ReelGalleryRow> rows, IReadOnlyList<CameraReelResponseCompact> reels, in ReelGalleryConfigParams config,
            float topPadding, float bottomPadding, float monthSpacing)
        {
            rows.Clear();
            rows.Add(Padding(topPadding));

            int columns = config.GridLayoutFixedColumnCount;
            int sectionStart = 0;

            while (sectionStart < reels.Count)
            {
                DateTime month = ReelUtility.GetImageDateTime(reels[sectionStart]);
                int sectionEnd = reels.Count;

                if (config.GroupByMonth)
                {
                    sectionEnd = sectionStart + 1;

                    while (sectionEnd < reels.Count && ReelUtility.GetImageDateTime(reels[sectionEnd]) == month)
                        sectionEnd++;
                }

                if (config.GridShowMonth)
                    rows.Add(MonthHeader(month));

                for (int start = sectionStart; start < sectionEnd; start += columns)
                {
                    int count = Math.Min(columns, sectionEnd - start);
                    bool lastInSection = start + count >= sectionEnd;
                    float spacingAfter = !lastInSection ? ROW_SPACING : sectionEnd >= reels.Count ? 0f : monthSpacing;
                    rows.Add(Thumbnails(start, count, spacingAfter));
                }

                sectionStart = sectionEnd;
            }

            rows.Add(Padding(bottomPadding));
        }
    }
}
