using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Sentry;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Submits one bug report end to end: first the Sentry User Feedback entry carrying the
    ///     user's first image and the client log, then the Intercom ticket whose description links to it.
    /// </summary>
    public class BugReportService
    {
        private const string TITLE_PREFIX = "Bug Report: ";
        private const string DIAGNOSTICS_LABEL = "Internal diagnostics: ";
        private const string DIAGNOSTICS_UNAVAILABLE = "unavailable";
        private const string COORDINATES_LABEL = "Coordinates: ";

        private readonly SentryUserFeedbackService feedbackService;
        private readonly IntercomTicketClient ticketClient;

        public BugReportService(SentryUserFeedbackService feedbackService, IntercomTicketClient ticketClient)
        {
            this.feedbackService = feedbackService;
            this.ticketClient = ticketClient;
        }

        /// <returns>The id of the created Intercom ticket.</returns>
        public virtual async UniTask<Result<string>> SubmitAsync(BugReportInput input, CancellationToken ct)
        {
            // A feedback envelope carries a single attachment, so Sentry gets the first image only.
            EvidenceImage? firstImage = input.Images is { Count: > 0 } ? input.Images[0] : null;

            var feedbackReport = new SentryUserFeedbackReport(
                $"[{input.IssueType.Label}] {input.Description}",
                input.ContactEmail,
                input.UserName,
                firstImage?.Bytes,
                firstImage?.ContentType);

            Result<string> feedbackLink = await feedbackService.SubmitAsync(feedbackReport, ct);

            if (ct.IsCancellationRequested)
                return Result<string>.CancelledResult();

            if (!feedbackLink.Success)
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Bug report proceeds without Sentry diagnostics: {feedbackLink.ErrorMessage}");

            var ticket = new IntercomTicketData
            {
                Title = $"{TITLE_PREFIX}{input.IssueType.Label}",
                Description = ComposeTicketDescription(input.Description, input.Coordinates, feedbackLink.Success ? feedbackLink.Value : null),
                IssueTypeOptionId = input.IssueType.OptionId,
                OperatingSystem = SystemInfo.operatingSystem,
                GraphicCard = SystemInfo.graphicsDeviceName,
                Ram = $"{SystemInfo.systemMemorySize} MB",
                ClientVersion = Application.version,

                // Explorer ships for desktop only.
                Platform = IntercomTicketPlatform.Desktop,
                SdkVersion = input.SceneSdkVersion,
                LauncherVersion = input.LauncherVersion,
                MeetsMinimumRequirementsOptionId = MinimumSpecOptionId(input.MeetsMinimumSpecs),
                Evidence = SelectEvidenceImages(input.Images),
            };

            return await ticketClient.CreateTicketAsync(ticket, ct);
        }

        public static string? MinimumSpecOptionId(bool? meetsMinimumSpecs) =>
            meetsMinimumSpecs == null
                ? null
                : meetsMinimumSpecs.Value
                    ? BugReportMinimumSpecOptions.MEETS_MIN_SPEC
                    : BugReportMinimumSpecOptions.BELOW_MIN_SPEC;

        /// <summary>
        ///     The proxy rejects the whole ticket over an oversized image, a fourth image or an oversized request,
        ///     so every image that would trip one of those caps is dropped from the ticket instead. Order is kept.
        /// </summary>
        public static IReadOnlyList<EvidenceImage> SelectEvidenceImages(IReadOnlyList<EvidenceImage>? images)
        {
            if (images == null || images.Count == 0)
                return Array.Empty<EvidenceImage>();

            var selected = new List<EvidenceImage>(Math.Min(images.Count, IntercomTicketPayload.MAX_EVIDENCE_IMAGES));
            var totalBytes = 0;

            for (var i = 0; i < images.Count; i++)
            {
                int length = images[i].Bytes.Length;

                if (length == 0)
                    continue;

                if (selected.Count == IntercomTicketPayload.MAX_EVIDENCE_IMAGES)
                {
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Only the first {IntercomTicketPayload.MAX_EVIDENCE_IMAGES} attached images travel with the ticket: the rest are dropped");
                    break;
                }

                if (length > IntercomTicketPayload.MAX_EVIDENCE_BYTES)
                {
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Attached image {i + 1} exceeds the {IntercomTicketPayload.MAX_EVIDENCE_BYTES / (1024 * 1024)}MB ticket evidence cap: it is dropped from the ticket");
                    continue;
                }

                if (totalBytes + length > IntercomTicketPayload.MAX_EVIDENCE_TOTAL_BYTES)
                {
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Attached image {i + 1} does not fit in the {IntercomTicketPayload.MAX_EVIDENCE_TOTAL_BYTES / (1024 * 1024)}MB ticket evidence budget: it is dropped from the ticket");
                    continue;
                }

                selected.Add(images[i]);
                totalBytes += length;
            }

            return selected;
        }

        public static string ComposeTicketDescription(string description, Vector2Int? coordinates, string? feedbackLink)
        {
            var builder = new StringBuilder(description);
            builder.Append("\n\n---");

            if (coordinates != null)
                builder.Append('\n').Append(COORDINATES_LABEL).Append(coordinates.Value.x).Append(',').Append(coordinates.Value.y);

            builder.Append('\n').Append(DIAGNOSTICS_LABEL).Append(feedbackLink ?? DIAGNOSTICS_UNAVAILABLE);

            return builder.ToString();
        }
    }
}
