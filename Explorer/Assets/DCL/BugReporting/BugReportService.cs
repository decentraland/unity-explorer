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
        // The "Bug Report" ticket type in the Decentraland Intercom workspace.
        private const long BUG_REPORT_TICKET_TYPE_ID = 4557778;
        private const string TITLE_PREFIX = "Bug Report: ";
        private const string DIAGNOSTICS_LABEL = "Internal diagnostics (Sentry, dev team only): ";
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

            if (!feedbackLink.Success)
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Bug report proceeds without Sentry diagnostics: {feedbackLink.ErrorMessage}");

            if (ct.IsCancellationRequested)
                return Result<string>.CancelledResult();

            var ticket = new IntercomTicketData
            {
                TicketTypeId = BUG_REPORT_TICKET_TYPE_ID,
                Title = $"{TITLE_PREFIX}{input.IssueType.Label}",
                Description = ComposeTicketDescription(input.Description, input.Coordinates, feedbackLink.Success ? feedbackLink.Value : null),
                IssueTypeOptionId = input.IssueType.OptionId,
                OperatingSystem = SystemInfo.operatingSystem,
                GraphicCard = SystemInfo.graphicsDeviceName,
                Ram = $"{SystemInfo.systemMemorySize} MB",
                ClientVersion = Application.version,
            };

            return await ticketClient.CreateTicketAsync(ticket, ct);
        }

        /// <summary>
        ///     The Sentry link rides in the ticket body because the type's files attributes
        ///     (Evidence, Player Logs) reject API writes.
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
