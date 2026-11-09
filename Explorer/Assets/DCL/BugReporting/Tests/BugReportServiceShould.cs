using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.BugReporting.Tests
{
    public class BugReportServiceShould
    {
        private const string DESCRIPTION = "The avatar falls through the floor.";
        private const string LINK = "https://decentraland.sentry.io/issues/feedback/?projectSlug=explorer&eventId=80f9a06b97e94d8686cb232bb681f266";
        private const string CONTENT_TYPE = "image/png";

        [Test]
        public void IncludeFeedbackLinkInDescription()
        {
            // Act
            string composed = BugReportService.ComposeTicketDescription(DESCRIPTION, null, LINK);

            // Assert
            StringAssert.StartsWith(DESCRIPTION, composed);
            StringAssert.Contains(LINK, composed);
        }

        [Test]
        public void FallBackWhenFeedbackLinkIsMissing()
        {
            // Act
            string composed = BugReportService.ComposeTicketDescription(DESCRIPTION, null, null);

            // Assert
            StringAssert.StartsWith(DESCRIPTION, composed);
            StringAssert.Contains("unavailable", composed);
        }

        [Test]
        public void IncludeCoordinatesWhenProvided()
        {
            // Act
            string composed = BugReportService.ComposeTicketDescription(DESCRIPTION, new Vector2Int(121, -34), LINK);

            // Assert
            StringAssert.Contains("Coordinates: 121,-34", composed);
        }

        [Test]
        public void MapTheMinimumSpecOutcomeToItsListOption()
        {
            Assert.AreEqual(BugReportMinimumSpecOptions.MEETS_MIN_SPEC, BugReportService.MinimumSpecOptionId(true));
            Assert.AreEqual(BugReportMinimumSpecOptions.BELOW_MIN_SPEC, BugReportService.MinimumSpecOptionId(false));
            Assert.IsNull(BugReportService.MinimumSpecOptionId(null));
        }

        [Test]
        public void KeepImagesWithinTheEvidenceCapsInOrder()
        {
            // Arrange
            EvidenceImage first = Image(16);
            EvidenceImage second = Image(32);

            // Act
            IReadOnlyList<EvidenceImage> selected = BugReportService.SelectEvidenceImages(new[] { first, second });

            // Assert
            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(first.Bytes, selected[0].Bytes);
            Assert.AreSame(second.Bytes, selected[1].Bytes);
        }

        [Test]
        public void SelectNothingWithoutImages()
        {
            Assert.AreEqual(0, BugReportService.SelectEvidenceImages(null).Count);
            Assert.AreEqual(0, BugReportService.SelectEvidenceImages(Array.Empty<EvidenceImage>()).Count);
            Assert.AreEqual(0, BugReportService.SelectEvidenceImages(new[] { Image(0) }).Count);
        }

        [Test]
        public void DropAnImageAboveThePerImageCapAndKeepTheRest()
        {
            // Arrange
            EvidenceImage oversized = Image(IntercomTicketPayload.MAX_EVIDENCE_BYTES + 1);
            EvidenceImage small = Image(16);

            // Act
            IReadOnlyList<EvidenceImage> selected = BugReportService.SelectEvidenceImages(new[] { oversized, small });

            // Assert
            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(small.Bytes, selected[0].Bytes);
        }

        [Test]
        public void DropImagesBeyondTheProxyLimit()
        {
            // Arrange
            var images = new EvidenceImage[IntercomTicketPayload.MAX_EVIDENCE_IMAGES + 1];

            for (var i = 0; i < images.Length; i++)
                images[i] = Image(16 + i);

            // Act
            IReadOnlyList<EvidenceImage> selected = BugReportService.SelectEvidenceImages(images);

            // Assert - the first ones win: the user attached them first.
            Assert.AreEqual(IntercomTicketPayload.MAX_EVIDENCE_IMAGES, selected.Count);

            for (var i = 0; i < selected.Count; i++)
                Assert.AreSame(images[i].Bytes, selected[i].Bytes);
        }

        [Test]
        public void DropAnImageThatOverflowsTheTotalBudgetAndKeepALaterOneThatFits()
        {
            // Arrange
            int twoThirds = IntercomTicketPayload.MAX_EVIDENCE_TOTAL_BYTES / 3 * 2;
            EvidenceImage first = Image(twoThirds);
            EvidenceImage second = Image(twoThirds);
            EvidenceImage third = Image(16);

            // Act
            IReadOnlyList<EvidenceImage> selected = BugReportService.SelectEvidenceImages(new[] { first, second, third });

            // Assert
            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(first.Bytes, selected[0].Bytes);
            Assert.AreSame(third.Bytes, selected[1].Bytes);
        }

        private static EvidenceImage Image(int length) =>
            new (new byte[length], CONTENT_TYPE);
    }
}
