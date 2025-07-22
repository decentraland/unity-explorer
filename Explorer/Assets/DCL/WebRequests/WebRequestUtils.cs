using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public static class WebRequestUtils
    {
        public const string CANNOT_CONNECT_ERROR = "Cannot connect to destination host";

        public const int BAD_REQUEST = 400;
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

        public static (bool canBeRepeated, TimeSpan retryDelay) CanBeRepeated(int attemptNumber, RetryPolicy retryPolicy, bool idempotent, UnityWebRequestException webRequestException)
        {
            // Retries count are exhausted (attemptNumber is 1-based)
            if (attemptNumber > retryPolicy.maxRetriesCount)
                return (false, TimeSpan.Zero);

            // Unless repetitions are enforced, non-idempotent requests should not be retried
            if (!idempotent && !retryPolicy.enforced)
                return (false, TimeSpan.Zero);

            // Handle "Retry-After" header. Applicable for 429 Too Many Requests and 503 Service Unavailable
            if (webRequestException.ResponseCode is 429 or 503)
            {
                // "Retry-After" header is not present or not parsable, don't repeat
                if (webRequestException.ResponseHeaders.TryGetValue("Retry-After", out string? retryAfterHeader) || retryAfterHeader is null)
                    return (true, TimeSpan.Zero);

                // Can be a date or seconds
                if (int.TryParse(retryAfterHeader, out int retryAfter))
                    return (true, TimeSpan.FromSeconds(retryAfter));

                // For .NET/Unity, use the built‑in RFC1123 pattern ("r" or "R"):

                if (DateTime.TryParseExact(
                        retryAfterHeader,
                        "r", // RFC1123 aka IMF-fixdate
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime retryDateUtc))
                {
                    TimeSpan timeToWait = retryDateUtc - DateTime.UtcNow;

                    // Time already passed, no need to wait
                    if (timeToWait <= TimeSpan.Zero)
                        return (true, TimeSpan.Zero);

                    // Should not exceed the maximum delay
                    if (timeToWait.TotalMilliseconds > RetryPolicy.MAX_DELAY_BETWEEN_ATTEMPTS_MS)

                        // Can't retry straight-away automatically
                        return (false, TimeSpan.Zero);

                    return (true, timeToWait);
                }

                // Invalid "Retry-After" header format, don't repeat
                return (false, TimeSpan.Zero);
            }

            // The default scheme
            if (webRequestException.IsIrrecoverableError())
                return (false, TimeSpan.Zero);

            return (true, GetRetryDelay());

            TimeSpan GetRetryDelay()
            {
                double factor = Math.Pow(retryPolicy.backoffMultiplier, attemptNumber - 1);
                return TimeSpan.FromMilliseconds(Math.Min(retryPolicy.minDelayBetweenAttemptsMs * factor, RetryPolicy.MAX_DELAY_BETWEEN_ATTEMPTS_MS));
            }
        }

        public static bool IsDNSLookupError(this UnityWebRequestException exception) =>
            exception.ResponseCode == 0 && exception.Message.Contains(CANNOT_CONNECT_ERROR);

        public static bool IsIrrecoverableError(this UnityWebRequestException exception)
        {
            if (exception.IsDNSLookupError())
                return false;

            return (exception.IsAborted() || IsIrrecoverableServerError(exception.ResponseCode) || IsIrrecoverableStructuralError(exception.ResponseCode))
                   && !exception.IsUnableToCompleteSSLConnection();
        }

        private static bool IsIrrecoverableStructuralError(long responseCode)
        {
            switch (responseCode)
            {
                case 408: // Request Timeout
                case 425: // Too Early
                case 429: // Too Many Requests
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsIrrecoverableServerError(long responseCode)
        {
            switch (responseCode)
            {
                case 501: // Not Implemented. Transient server bug/outage
                case 505: // HTTP Version Not Supported
                case 508: // Loop Detected
                case 507: // Insufficient Storage.
                case 511: // Network Authentication Required
                    return true;
                case 500: // Internal Server Error
                case 502: // Bad Gateway. Reverse proxy/CDN got a bad response from upstream
                case 503: // Service Unavailable. Overload, maintenance window
                case 504: // Gateway Timeout. Upstream didn’t respond in time
                    return false;
                default:
                    return true;
            }
        }

        public static bool IsUnableToCompleteSSLConnection(this UnityWebRequestException exception)
        {
            // fixes frequent editor exception
#if UNITY_EDITOR
            return exception.Message.Contains("Unable to complete SSL connection");
#else
            return false;
#endif
        }

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        public static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };

        public static string GetResponseContentType(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Type");

        public static string GetResponseContentEncoding(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Encoding");

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
