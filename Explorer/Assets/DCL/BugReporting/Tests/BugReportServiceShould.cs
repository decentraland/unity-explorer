using NUnit.Framework;
using UnityEngine;

namespace DCL.BugReporting.Tests
{
    public class BugReportServiceShould
    {
        private const string DESCRIPTION = "The avatar falls through the floor.";
        private const string LINK = "https://decentraland.sentry.io/issues/feedback/?projectSlug=explorer&eventId=80f9a06b97e94d8686cb232bb681f266";

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
        public void KeepAnImageWithinTheEvidenceCap()
        {
            // Arrange
            var image = new byte[16];

            // Assert
            Assert.AreSame(image, BugReportService.SelectEvidenceImage(image));
            Assert.IsNull(BugReportService.SelectEvidenceImage(null));
        }

        [Test]
        public void DropAnImageAboveTheEvidenceCap()
        {
            // Arrange
            var image = new byte[IntercomTicketPayload.MAX_EVIDENCE_BYTES + 1];

            // Assert
            Assert.IsNull(BugReportService.SelectEvidenceImage(image));
        }
    }
}
