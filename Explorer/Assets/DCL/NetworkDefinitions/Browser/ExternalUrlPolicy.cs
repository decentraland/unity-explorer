using System;

namespace DCL.Browser
{
    /// <summary>
    /// Central policy for opening external URLs: only http/https reach the OS handler. Custom schemes
    /// (decentraland://, smb://, file://, mailto:, app-protocol handlers) are refused so a scene/profile
    /// link can never re-invoke the launcher (SEC-028) or leak NTLM/local files (SEC-008).
    /// </summary>
    public static class ExternalUrlPolicy
    {
        /// <summary>
        /// True if the URL is an absolute http/https web URL. Validates the scheme by prefix and does NOT
        /// allocate a <see cref="Uri"/> (callers that already hold a parsed Uri use it directly instead).
        /// This is the single http/https predicate shared across the client — external-URL sink, realm
        /// resolution, and realm launch settings all route through it.
        /// </summary>
        public static bool IsWebScheme(string? url) =>
            !string.IsNullOrEmpty(url)
            && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        /// <summary>Trust-cache key on (scheme, host). Returns false for authority-less URIs (empty host),
        /// which must never be cached (SEC-008 empty-host bypass).</summary>
        public static bool TryGetTrustKey(Uri uri, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrEmpty(uri.Host))
                return false;

            key = $"{uri.Scheme}|{uri.Host}";
            return true;
        }
    }
}
