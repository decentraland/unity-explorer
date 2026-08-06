using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Diagnostics.Sentry;
using DCL.Utility.Types;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Submits one bug report end to end: first the Sentry User Feedback entry carrying the
    ///     user's image and the client log, then the Intercom ticket whose description links to it.
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
            var feedbackReport = new SentryUserFeedbackReport(
                $"[{input.IssueType.Label}] {input.Description}",
                input.ContactEmail,
                input.UserName,
                input.Image,
                input.ImageContentType,
                input.ShareLogs);

            // A Sentry failure never blocks the ticket: the description degrades to a note instead.
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
                SdkVersion = input.SceneSdkVersion,
                LauncherVersion = input.LauncherVersion,
                MeetsMinimumRequirementsOptionId = MinimumSpecOptionId(input.MeetsMinimumSpecs),
                EvidenceImage = SelectEvidenceImage(input.Image),
                EvidenceContentType = input.ImageContentType,
            };

            return await ticketClient.CreateTicketAsync(ticket, ct);
        }

        /// <summary>
        ///     The hardware check yields a boolean, so only the two spec options it can tell apart
        ///     are ever sent; an unknown outcome leaves the attribute out of the ticket.
        /// </summary>
        public static string? MinimumSpecOptionId(bool? meetsMinimumSpecs) =>
            meetsMinimumSpecs == null
                ? null
                : meetsMinimumSpecs.Value
                    ? BugReportMinimumSpecOptions.MEETS_MIN_SPEC
                    : BugReportMinimumSpecOptions.BELOW_MIN_SPEC;

        /// <summary>
        ///     The proxy rejects the whole ticket over an oversized image, so one degrades to the
        ///     Sentry copy instead: the description's diagnostics link still leads to it.
        /// </summary>
        public static byte[]? SelectEvidenceImage(byte[]? image)
        {
            if (image is not { Length: > IntercomTicketPayload.MAX_EVIDENCE_BYTES })
                return image;

            ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"The attached image exceeds the {IntercomTicketPayload.MAX_EVIDENCE_BYTES / (1024 * 1024)}MB ticket evidence cap: it travels to Sentry only");
            return null;
        }

        /// <summary>
        ///     Coordinates and the diagnostics link ride in the ticket body: the Bug Report ticket
        ///     type declares no attribute for either.
        /// </summary>
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
