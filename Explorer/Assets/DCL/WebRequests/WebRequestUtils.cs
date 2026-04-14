using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Globalization;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public static class WebRequestUtils
    {
        public const string CANNOT_CONNECT_ERROR = "Cannot connect to destination host";

        public const int BAD_REQUEST = 400;
        public const int UNAUTHORIZED_ACCESS = 401;
        public const int FORBIDDEN_ACCESS = 403;
        public const int NOT_FOUND = 404;

        public static SuppressExceptionWithFallback<TCoreOp, TWebRequest, TResult> SuppressExceptionsWithFallback<TCoreOp, TWebRequest, TResult>(this TCoreOp coreOp, TResult fallbackValue, SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default, ReportData? reportContext = null) where TWebRequest: struct, ITypedWebRequest where TCoreOp: IWebRequestOp<TWebRequest, TResult> =>
            new (coreOp, fallbackValue, behaviour, reportContext);

        public static async UniTask<T> WithCustomExceptionAsync<T>(this UniTask<T> webRequestFlow, Func<UnityWebRequestException, Exception> newExceptionFactoryMethod)
        {
            try { return await webRequestFlow; }
            catch (UnityWebRequestException e) { throw newExceptionFactoryMethod(e); }
        }

        public static bool IsIdempotent<TWebRequest>(this TWebRequest webRequest, in WebRequestSignInfo? signInfo) where TWebRequest: ITypedWebRequest =>

            // Requests with a signature are not idempotent due to the possible signature expiration
            webRequest.Idempotent && !signInfo.HasValue;

        public static (bool canBeRepeated, TimeSpan retryDelay) CanBeRepeated(int attemptNumber, RetryPolicy retryPolicy, bool idempotent, UnityWebRequestException? webRequestException)
        {
            // Retries count are exhausted (attemptNumber is 1-based)
            if (attemptNumber > retryPolicy.maxRetriesCount)
                return (false, TimeSpan.Zero);

            // Unless repetitions are enforced, non-idempotent requests should not be retried
            if (!idempotent && retryPolicy.strictness != RetryPolicy.Strictness.ENFORCED)
                return (false, TimeSpan.Zero);

            // Handle "Retry-After" header. Applicable for 429 Too Many Requests and 503 Service Unavailable
            if (webRequestException?.ResponseCode is 429 or 503)
            {
                // "Retry-After" header is not present or not parsable, don't repeat
                if (!webRequestException.ResponseHeaders.TryGetValue("Retry-After", out string? retryAfterHeader) || retryAfterHeader is null)
                    return (false, TimeSpan.Zero);

                TimeSpan retryDelay;

                // Can be a date or seconds
                if (int.TryParse(retryAfterHeader, out int retryAfter))
                    retryDelay = TimeSpan.FromSeconds(retryAfter);

                // For .NET/Unity, use the built‑in RFC1123 pattern ("r" or "R"):

                else if (DateTime.TryParseExact(
                             retryAfterHeader,
                             "r", // RFC1123 aka IMF-fixdate
                             CultureInfo.InvariantCulture,
                             DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                             out DateTime retryDateUtc))
                {
                    retryDelay = retryDateUtc - DateTime.UtcNow;

                    // Time already passed, no need to wait
                    if (retryDelay <= TimeSpan.Zero)
                        return (true, TimeSpan.Zero);
                }
                else

                    // Invalid "Retry-After" header format, don't repeat
                    return (false, TimeSpan.Zero);

                // Should not exceed the maximum delay
                if (retryDelay.TotalMilliseconds > RetryPolicy.MAX_DELAY_BETWEEN_ATTEMPTS_MS)

                    // Can't retry straight-away automatically
                    return (false, TimeSpan.Zero);

                return (true, retryDelay);
            }

            if (retryPolicy.strictness == RetryPolicy.Strictness.RETRY_AFTER_REQUIRED)

                // If default policy is not applied, return immediately
                return (false, TimeSpan.Zero);

            // The default scheme
            if (webRequestException != null)
            {
                bool errorCodeIsExpected = retryPolicy.forceRecoverableCodes?.Contains(webRequestException.ResponseCode) ?? false;

                if (!errorCodeIsExpected && webRequestException.IsIrrecoverableError())
                    return (false, TimeSpan.Zero);
            }

            return (true, GetRetryDelay());

            TimeSpan GetRetryDelay()
            {
                double factor = Math.Pow(retryPolicy.backoffMultiplier, attemptNumber - 1);
                return TimeSpan.FromMilliseconds(Math.Min(retryPolicy.minDelayBetweenAttemptsMs * factor, RetryPolicy.MAX_DELAY_BETWEEN_ATTEMPTS_MS));
            }
        }

        private static bool IsDNSLookupError(this UnityWebRequestException exception) =>
            exception.ResponseCode == 0 && exception.Message.Contains(CANNOT_CONNECT_ERROR);

        public static bool IsIrrecoverableError(this UnityWebRequestException exception)
        {
            if (exception.IsDNSLookupError())
                return false;

            return (exception.IsAborted() || IsIrrecoverableResponseCode(exception.ResponseCode))
                   && !exception.IsUnableToCompleteSSLConnection()
                   && !exception.IsSSLCACertificateError();
        }

        private static bool IsIrrecoverableResponseCode(long responseCode)
        {
            switch (responseCode)
            {
                // Recoverable client errors
                case 408: // Request Timeout
                case 425: // Too Early
                case 429: // Too Many Requests

                // Recoverable server errors
                case 500: // Internal Server Error
                case 502: // Bad Gateway — reverse proxy/CDN got a bad response from upstream
                case 503: // Service Unavailable — overload, maintenance window
                case 504: // Gateway Timeout — upstream didn’t respond in time

                // Recoverable CDN-specific errors (transient)
                case 521: // Web Server Is Down
                case 522: // Connection Timed Out
                case 523: // Origin Is Unreachable
                case 524: // A Timeout Occurred — Cloudflare equivalent of 504
                case 525: // SSL Handshake Failed
                    return false;

                // Everything else is irrecoverable (4xx client errors, permanent 5xx like 501/505/507/508/511, etc.)
                default:
                    return true;
            }
        }

        private static bool IsUnableToCompleteSSLConnection(this UnityWebRequestException exception) =>
            exception.Message.Contains("Unable to complete SSL connection");

        private static bool IsSSLCACertificateError(this UnityWebRequestException exception) =>
            exception.Message.Contains("SSL CA certificate error");

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        private static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };

        public static string GetResponseContentType(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Type");

        public static string GetResponseContentEncoding(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Encoding");

        public static bool IsLocalhost(string url) =>
            url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://[::1]", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://[::1]", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Does nothing with the web request
        /// </summary>
        public readonly struct NoOp<TWebRequest> : IWebRequestOp<TWebRequest, NoResult> where TWebRequest: struct, ITypedWebRequest
        {
            public UniTask<NoResult> ExecuteAsync(TWebRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(new NoResult());
        }

        public readonly struct NoResult { }
    }
}
