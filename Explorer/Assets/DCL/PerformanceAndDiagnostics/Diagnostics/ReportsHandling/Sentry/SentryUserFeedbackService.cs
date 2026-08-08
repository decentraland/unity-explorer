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
    /// </summary>
    public class SentryUserFeedbackService
    {
        private const string LOG_EVENT_MESSAGE = "Bug report log attachment";
        private const string LOG_CONTENT_TYPE = "text/plain";
        private const string JPG_CONTENT_TYPE = "image/jpeg";
        private const string PNG_CONTENT_TYPE = "image/png";
        private const string JPG_FILE_NAME = "screenshot.jpg";
        private const string PNG_FILE_NAME = "screenshot.png";
        private const string CATEGORY_TAG_VALUE = "FEEDBACK";
        private const int LOG_TAIL_MAX_BYTES = 64 * 1024;
        private const int IMAGE_MAX_BYTES = 10 * 1024 * 1024;

        private static readonly TimeSpan FLUSH_TIMEOUT = TimeSpan.FromSeconds(5);

        private readonly string feedbackUrlTemplate;
        private readonly Action<Scope> configureScopeCached;
        private readonly Action<Scope>? externalConfigureScope;

        /// <param name="feedbackUrlTemplate">Deep link into the Sentry feedback UI, with {0} standing for the feedback event id.</param>
        /// <param name="configureScope">Optional enrichment applied to the feedback and log events, e.g. wallet and scene tags.</param>
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

            // Every capture call is a silent no-op while the hub is disabled, so refuse to report success.
            if (!SentrySdk.IsEnabled)
                return Result<string>.ErrorResult("Sentry is not initialized");

            if (string.IsNullOrWhiteSpace(report.Message))
                return Result<string>.ErrorResult("The report message is empty");

            if (report.Image is { Length: > IMAGE_MAX_BYTES })
                return Result<string>.ErrorResult($"The image exceeds {IMAGE_MAX_BYTES / (1024 * 1024)}MB");

            // A feedback envelope keeps a single attachment, so the log travels on an event of its
            // own, which the feedback points at through its associated event id.
            SentryId logEventId = CaptureLogEvent();

            SentryHint? hint = null;

            if (report.Image != null)
            {
                string contentType = report.ImageContentType ?? JPG_CONTENT_TYPE;
                hint = new SentryHint();
                hint.AddAttachment(report.Image, contentType == PNG_CONTENT_TYPE ? PNG_FILE_NAME : JPG_FILE_NAME, AttachmentType.Default, contentType);
            }

            var feedback = new SentryFeedback(
                report.Message,
                NullIfEmpty(report.ContactEmail),
                NullIfEmpty(report.UserName),
                replayId: null,
                url: null,
                associatedEventId: logEventId == SentryId.Empty ? null : logEventId);

            // The Sentry.Unity facade returns void from CaptureFeedback: HubAdapter is the public
            // surface that hands back the event id the deep link needs.
            SentryId feedbackId = HubAdapter.Instance.CaptureFeedback(feedback, out CaptureFeedbackResult result, configureScopeCached, hint);

            if (result != CaptureFeedbackResult.Success)
                return Result<string>.ErrorResult($"Sentry rejected the feedback: {result}");

            // The deep link resolves only once the envelope reaches Sentry, so delivery is awaited
            // before the link is handed out. A timed-out flush still delivers later, so it is not an
            // error, and a cancellation cannot recall the captured feedback: success is reported anyway.
            await FlushAsync();

            return Result<string>.SuccessResult(string.Format(feedbackUrlTemplate, feedbackId));
        }

        private SentryId CaptureLogEvent()
        {
            (string fileName, byte[] bytes)? tail = ReadLogTail(LOG_TAIL_MAX_BYTES);

            if (tail == null)
                return SentryId.Empty;

            return SentrySdk.CaptureMessage(LOG_EVENT_MESSAGE, scope =>
            {
                configureScopeCached(scope);
                scope.AddAttachment(tail.Value.bytes, tail.Value.fileName, AttachmentType.Default, LOG_CONTENT_TYPE);
            });
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
