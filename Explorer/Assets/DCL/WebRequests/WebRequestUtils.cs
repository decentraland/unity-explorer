using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public static class WebRequestUtils
    {
        public const string CANNOT_CONNECT_ERROR = "Cannot connect to destination host";

        private const string HTTP_SCHEME_PREFIX = "http://";
        private const string HTTPS_SCHEME_PREFIX = "https://";

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
            if (!idempotent && retryPolicy.strictness != RetryPolicy.Strictness.Enforced)
                return (false, TimeSpan.Zero);

            // Handle "Retry-After" header. Applicable for 429 Too Many Requests and 503 Service Unavailable
            if (webRequestException?.ResponseCode is 429 or 503)
            {
                // "Retry-After" header is not present or not parsable, don't repeat
                if (webRequestException.ResponseHeaders == null
                    || !webRequestException.ResponseHeaders.TryGetValue("Retry-After", out string? retryAfterHeader)
                    || retryAfterHeader is null)
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

            if (retryPolicy.strictness == RetryPolicy.Strictness.RetryAfterRequired)

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

        private static bool IsDnsLookupError(this UnityWebRequestException exception) =>
            exception.ResponseCode == 0 && exception.Message.Contains(CANNOT_CONNECT_ERROR);

        public static bool IsIrrecoverableError(this UnityWebRequestException exception)
        {
            if (exception.IsDnsLookupError())
                return false;

            return (exception.IsAborted() || IsIrrecoverableResponseCode(exception.ResponseCode))
                   && !exception.IsUnableToCompleteSslConnection()
                   && !exception.IsSslCaCertificateError();
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

        private static bool IsUnableToCompleteSslConnection(this UnityWebRequestException exception) =>
            exception.Message.Contains("Unable to complete SSL connection");

        private static bool IsSslCaCertificateError(this UnityWebRequestException exception) =>
            exception.Message.Contains("SSL CA certificate error");

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        private static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };

        public static string GetResponseContentType(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Type");

        public static string GetResponseContentEncoding(this UnityWebRequest unityWebRequest) =>
            unityWebRequest.GetResponseHeader("Content-Encoding");

        /// <summary>
        ///     Client-side transport-security policy, standing in for the player-level insecure-http
        ///     block (which is global and cannot exempt loopback): cleartext http is permitted to
        ///     loopback hosts only (local preview servers, sidecars); http to any other host is
        ///     upgraded to https. Binds on the wire URL of every envelope whose request did not opt
        ///     into cleartext (<see cref="CommonArguments.AllowInsecureCleartext" />) and where a
        ///     policed infra URL is resolved (media resolution, sidecar realm root) — never to URLs
        ///     embedded in a request as data. Non-http URLs pass through unchanged (same reference).
        /// </summary>
        public static string EnforceSecureScheme(string url) =>
            IsForbiddenCleartext(url)
                ? string.Concat(HTTPS_SCHEME_PREFIX, url.Substring(HTTP_SCHEME_PREFIX.Length))
                : url;

        /// <summary>
        ///     True only for the scheme/host combination the transport policy forbids: cleartext
        ///     http to a non-loopback host. Unparsable http URLs are forbidden too — the policy
        ///     fails closed on cleartext. Holds for final (post-redirect) URLs as much as for
        ///     outgoing ones.
        /// </summary>
        public static bool IsForbiddenCleartext(string url) =>
            !string.IsNullOrEmpty(url)
            && url.StartsWith(HTTP_SCHEME_PREFIX, StringComparison.OrdinalIgnoreCase)
            && !(Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsLoopbackHost(uri.Host));

        /// <summary>
        ///     True when an exchange left on an allowed scheme/host but its final (post-redirect)
        ///     URL is forbidden cleartext. A request sent to forbidden cleartext in the first place
        ///     is not a downgrade: that scheme is the sender's own policy decision.
        /// </summary>
        public static bool IsCleartextDowngrade(string sentUrl, string finalUrl) =>
            !IsForbiddenCleartext(sentUrl) && IsForbiddenCleartext(finalUrl);

        /// <summary>
        ///     Loopback means "localhost", 127.0.0.0/8 or [::1] — the hosts a local preview
        ///     server or sidecar can be reached on.
        /// </summary>
        private static bool IsLoopbackHost(string host) =>
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out IPAddress? ip) && IPAddress.IsLoopback(ip));

        /// <summary>
        ///     Scheme-inclusive URL form of the single loopback definition (<see cref="IsLoopbackHost" />):
        ///     true only for http/https URLs whose parsed host is loopback. Non-http(s) schemes and
        ///     unparsable URLs are not localhost.
        /// </summary>
        public static bool IsLocalhost(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && IsLoopbackHost(uri.Host);

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
