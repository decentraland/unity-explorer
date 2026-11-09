using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.InWorldCamera.CameraReelGallery.Tests
{
    [TestFixture]
    public class ReelGalleryRowShould
    {
        private const float TOP = 30f;
        private const float BOTTOM = 12f;
        private const float MONTH_SPACING = 20f;

        private static readonly DateTime MARCH = new (2025, 3, 15, 12, 0, 0, DateTimeKind.Local);
        private static readonly DateTime FEBRUARY = new (2025, 2, 10, 12, 0, 0, DateTimeKind.Local);

        private readonly List<ReelGalleryRow> rows = new ();

        [Test]
        public void BuildHeaderAndChunkedRowsPerMonthWhenGrouped()
        {
            // Arrange
            List<CameraReelResponseCompact> reels = Reels((MARCH, 7), (FEBRUARY, 2));
            var config = new ReelGalleryConfigParams(6, 230, 306, gridShowMonth: true, groupByMonth: true);

            // Act
            ReelGalleryRow.Build(rows, reels, in config, TOP, BOTTOM, MONTH_SPACING);

            // Assert
            Assert.AreEqual(7, rows.Count);
            AssertPadding(rows[0], TOP);
            AssertHeader(rows[1], 2025, 3);
            AssertThumbnails(rows[2], 0, 6, ReelGalleryRow.ROW_SPACING);
            AssertThumbnails(rows[3], 6, 1, MONTH_SPACING);
            AssertHeader(rows[4], 2025, 2);
            AssertThumbnails(rows[5], 7, 2, 0f);
            AssertPadding(rows[6], BOTTOM);
        }

        [Test]
        public void NotSplitByMonthWhenNotGrouped()
        {
            // Arrange
            List<CameraReelResponseCompact> reels = Reels((MARCH, 5), (FEBRUARY, 4));
            var config = new ReelGalleryConfigParams(4, 201, 272, gridShowMonth: false, groupByMonth: false);

            // Act
            ReelGalleryRow.Build(rows, reels, in config, TOP, BOTTOM, MONTH_SPACING);

            // Assert
            Assert.AreEqual(5, rows.Count);
            AssertPadding(rows[0], TOP);
            AssertThumbnails(rows[1], 0, 4, ReelGalleryRow.ROW_SPACING);
            AssertThumbnails(rows[2], 4, 4, ReelGalleryRow.ROW_SPACING);
            AssertThumbnails(rows[3], 8, 1, 0f);
            AssertPadding(rows[4], BOTTOM);
        }

        [Test]
        public void BuildOnlyPaddingRowsForAnEmptyList()
        {
            // Arrange
            var config = new ReelGalleryConfigParams(6, 230, 306, gridShowMonth: true, groupByMonth: true);

            // Act
            ReelGalleryRow.Build(rows, new List<CameraReelResponseCompact>(), in config, TOP, BOTTOM, MONTH_SPACING);

            // Assert
            Assert.AreEqual(2, rows.Count);
            AssertPadding(rows[0], TOP);
            AssertPadding(rows[1], BOTTOM);
        }

        [Test]
        public void ShiftIndicesWhenAReelIsRemovedAndRowsAreRebuilt()
        {
            // Arrange
            List<CameraReelResponseCompact> reels = Reels((MARCH, 7));
            var config = new ReelGalleryConfigParams(6, 230, 306, gridShowMonth: true, groupByMonth: true);
            ReelGalleryRow.Build(rows, reels, in config, TOP, BOTTOM, MONTH_SPACING);
            Assert.AreEqual(5, rows.Count);

            // Act
            reels.RemoveAt(2);
            ReelGalleryRow.Build(rows, reels, in config, TOP, BOTTOM, MONTH_SPACING);

            // Assert
            Assert.AreEqual(4, rows.Count);
            AssertThumbnails(rows[2], 0, 6, 0f);
        }

        private static List<CameraReelResponseCompact> Reels(params (DateTime month, int count)[] sections)
        {
            var reels = new List<CameraReelResponseCompact>();

            foreach ((DateTime month, int count) in sections)
                for (var i = 0; i < count; i++)
                    reels.Add(new CameraReelResponseCompact
                    {
                        id = Guid.NewGuid().ToString(),
                        dateTime = new DateTimeOffset(month).ToUnixTimeSeconds().ToString(),
                    });

            return reels;
        }

        private static void AssertPadding(ReelGalleryRow row, float size)
        {
            Assert.AreEqual(ReelGalleryRowKind.Padding, row.Kind);
            Assert.AreEqual(size, row.Size);
        }

        private static void AssertHeader(ReelGalleryRow row, int year, int month)
        {
            Assert.AreEqual(ReelGalleryRowKind.MonthHeader, row.Kind);
            Assert.AreEqual(year, row.Month.Year);
            Assert.AreEqual(month, row.Month.Month);
            Assert.AreEqual(ReelGalleryRow.HEADER_SPACING, row.SpacingAfter);
        }

        private static void AssertThumbnails(ReelGalleryRow row, int startIndex, int count, float spacingAfter)
        {
            Assert.AreEqual(ReelGalleryRowKind.Thumbnails, row.Kind);
            Assert.AreEqual(startIndex, row.StartIndex);
            Assert.AreEqual(count, row.Count);
            Assert.AreEqual(spacingAfter, row.SpacingAfter);
        }
    }
}
