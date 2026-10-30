using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using Sentry;
using Sentry.Extensibility;
using Sentry.Unity;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DCL.Diagnostics.Sentry
{
    /// <summary>
    ///     Sends user-authored bug reports to Sentry User Feedback
    ///     (https://docs.sentry.io/product/user-feedback/) through the hub initialized by
    ///     <see cref="SentryReportHandler" />, and returns a deep link to the created entry.
    ///     The report travels as a feedback envelope only: it captures no event of its own, so it never opens a Sentry issue.
    /// </summary>
    public class SentryUserFeedbackService
    {
        private const string LOG_CONTENT_TYPE = "text/plain";
        private const string CATEGORY_TAG_VALUE = "FEEDBACK";
        private const int LOG_TAIL_MAX_BYTES = 64 * 1024;

        private static readonly TimeSpan FLUSH_TIMEOUT = TimeSpan.FromSeconds(5);

        private readonly string feedbackUrlTemplate;
        private readonly Action<Scope> configureScopeCached;
        private readonly Action<Scope>? externalConfigureScope;

        /// <param name="feedbackUrlTemplate">Deep link into the Sentry feedback UI, with {0} standing for the feedback event id.</param>
        /// <param name="configureScope">Optional enrichment applied to the feedback, e.g. wallet and scene tags.</param>
        public SentryUserFeedbackService(string feedbackUrlTemplate, Action<Scope>? configureScope = null)
        {
            this.feedbackUrlTemplate = feedbackUrlTemplate;
            externalConfigureScope = configureScope;
            configureScopeCached = ConfigureScope;
        }

        public virtual async UniTask<Result<string>> SubmitAsync(SentryUserFeedbackReport report, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return Result<string>.CancelledResult();

            if (!SentrySdk.IsEnabled)
                return Result<string>.ErrorResult("Sentry is not initialized");

            if (string.IsNullOrWhiteSpace(report.Message))
                return Result<string>.ErrorResult("The report message is empty");

            // A feedback envelope keeps a single attachment: the client log tail.
            SentryHint? hint = null;
            (string fileName, byte[] bytes)? logTail = ReadLogTail(LOG_TAIL_MAX_BYTES);

            if (logTail != null)
            {
                hint = new SentryHint();
                hint.AddAttachment(logTail.Value.bytes, logTail.Value.fileName, AttachmentType.Default, LOG_CONTENT_TYPE);
            }

            var feedback = new SentryFeedback(
                report.Message,
                NullIfEmpty(report.ContactEmail),
                NullIfEmpty(report.UserName));

            // HubAdapter instead of the Sentry.Unity facade: only it hands back the event id the deep link needs.
            SentryId feedbackId = HubAdapter.Instance.CaptureFeedback(feedback, out CaptureFeedbackResult result, configureScopeCached, hint);

            if (result != CaptureFeedbackResult.Success)
                return Result<string>.ErrorResult($"Sentry rejected the feedback: {result}");

            // The deep link resolves only once the envelope reaches Sentry; a timed-out flush still delivers later, so it is not an error.
            await FlushAsync();

            return Result<string>.SuccessResult(string.Format(feedbackUrlTemplate, feedbackId));
        }

        private void ConfigureScope(Scope scope)
        {
            scope.SetTag("category", CATEGORY_TAG_VALUE);
            externalConfigureScope?.Invoke(scope);
        }

        private static async UniTask FlushAsync()
        {
            try { await HubAdapter.Instance.FlushAsync(FLUSH_TIMEOUT).AsUniTask(); }
            catch (Exception e) { ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Sentry flush after feedback failed: {e.Message}"); }
        }

        private static (string fileName, byte[] bytes)? ReadLogTail(int maxBytes)
        {
            string path = Application.consoleLogPath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                // The engine keeps the log file open for writing, so it can only be read through a shared stream.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                long offset = Math.Max(0, stream.Length - maxBytes);
                stream.Seek(offset, SeekOrigin.Begin);

                var buffer = new byte[stream.Length - offset];
                int read = stream.Read(buffer, 0, buffer.Length);

                return (Path.GetFileName(path), read == buffer.Length ? buffer : buffer[..read]);
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"The log tail could not be read: {e.Message}");
                return null;
            }
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
