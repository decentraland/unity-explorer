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
        private const string DIAGNOSTICS_LABEL = "Internal diagnostics: ";
        private const string DIAGNOSTICS_UNAVAILABLE = "unavailable";
        private const string COORDINATES_LABEL = "Coordinates: ";
        private const string OS_LABEL = "OS: ";
        private const string GPU_LABEL = "GPU: ";
        private const string RAM_LABEL = "RAM: ";
        private const string CLIENT_VERSION_LABEL = "Client version: ";

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

            // The feedback service is exception-free and reports cancellation through its result,
            // so it is checked here rather than caught.
            if (ct.IsCancellationRequested)
                return Result<string>.CancelledResult();

            if (!feedbackLink.Success)
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Bug report proceeds without Sentry diagnostics: {feedbackLink.ErrorMessage}");

            var ticket = new IntercomTicketData
            {
                TicketTypeId = BUG_REPORT_TICKET_TYPE_ID,
                Title = $"{TITLE_PREFIX}{input.IssueType.Label}",
                Description = ComposeTicketDescription(input.Description, input.Coordinates, feedbackLink.Success ? feedbackLink.Value : null),
            };

            return await ticketClient.CreateTicketAsync(ticket, ct);
        }

        /// <summary>
        ///     All context rides in the ticket body: the proxy's workspace rejects attribute names
        ///     beyond the _default_title_/_default_description_ pseudo-attributes every type has.
        /// </summary>
        public static string ComposeTicketDescription(string description, Vector2Int? coordinates, string? feedbackLink)
        {
            var builder = new StringBuilder(description);
            builder.Append("\n\n---");

            if (coordinates != null)
                builder.Append('\n').Append(COORDINATES_LABEL).Append(coordinates.Value.x).Append(',').Append(coordinates.Value.y);

            builder.Append('\n').Append(DIAGNOSTICS_LABEL).Append(feedbackLink ?? DIAGNOSTICS_UNAVAILABLE);
            builder.Append('\n').Append(OS_LABEL).Append(SystemInfo.operatingSystem);
            builder.Append('\n').Append(GPU_LABEL).Append(SystemInfo.graphicsDeviceName);
            builder.Append('\n').Append(RAM_LABEL).Append(SystemInfo.systemMemorySize).Append(" MB");
            builder.Append('\n').Append(CLIENT_VERSION_LABEL).Append(Application.version);

            return builder.ToString();
        }
    }
}
