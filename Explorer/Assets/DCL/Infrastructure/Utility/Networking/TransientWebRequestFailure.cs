namespace Utility.Networking
{
    /// <summary>
    ///     Web request failures of the transport or the remote infrastructure, which a later attempt can succeed past, as opposed to definitive answers such as 4xx client errors.
    /// </summary>
    public static class TransientWebRequestFailure
    {
        public const string CANNOT_CONNECT_ERROR = "Cannot connect to destination host";
        private const string SSL_CONNECTION_ERROR = "Unable to complete SSL connection";
        private const string SSL_CA_CERTIFICATE_ERROR = "SSL CA certificate error";

        public static bool IsTransient(long responseCode, string? message) =>
            IsTransientResponseCode(responseCode) || IsConnectionFailure(responseCode, message);

        public static bool IsTransientResponseCode(long responseCode)
        {
            switch (responseCode)
            {
                case 408: // Request Timeout
                case 425: // Too Early
                case 429: // Too Many Requests

                case 500: // Internal Server Error
                case 502: // Bad Gateway
                case 503: // Service Unavailable
                case 504: // Gateway Timeout

                // Cloudflare-specific
                case 521: // Web Server Is Down
                case 522: // Connection Timed Out
                case 523: // Origin Is Unreachable
                case 524: // A Timeout Occurred
                case 525: // SSL Handshake Failed
                    return true;

                // 4xx client errors and permanent 5xx such as 501 or 505.
                default:
                    return false;
            }
        }

        /// <summary>
        ///     A connection that never produced a response has no code, only a transport message; an unsent request has neither.
        /// </summary>
        public static bool IsConnectionFailure(long responseCode, string? message) =>
            message != null
            && ((responseCode == 0 && message.Contains(CANNOT_CONNECT_ERROR))
                || message.Contains(SSL_CONNECTION_ERROR)
                || message.Contains(SSL_CA_CERTIFICATE_ERROR));
    }
}
