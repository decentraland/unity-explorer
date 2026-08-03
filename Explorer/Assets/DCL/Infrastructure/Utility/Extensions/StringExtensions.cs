using System;

namespace DCL.Utilities.Extensions
{
    public static class StringExtensions
    {
        private const string HTTP_SCHEME = "http://";
        private const string HTTPS_SCHEME = "https://";
        private const char SLASH = '/';

        public static bool IsValidUrl(this string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            ReadOnlySpan<char> urlSpan = url.AsSpan();

            // Check for the scheme
            ReadOnlySpan<char> httpSchemeSpan = HTTP_SCHEME.AsSpan();
            ReadOnlySpan<char> httpsSchemeSpan = HTTPS_SCHEME.AsSpan();
            bool isHttp = urlSpan.StartsWith(httpSchemeSpan);
            bool isHttps = urlSpan.StartsWith(httpsSchemeSpan);
            if (!isHttp && !isHttps)
                return false;

            ReadOnlySpan<char> restOfUrlSpan = urlSpan[(isHttp ? httpSchemeSpan.Length : httpsSchemeSpan.Length)..];
            if (!IsValid(restOfUrlSpan))
                return false;

            int domainEndIndex = restOfUrlSpan.IndexOf(SLASH);

            //Validates the rest of the url as domain when no SLASH char is found Ex: lvpr.tv?v=videoId
            if (domainEndIndex == -1)
                return IsValid(restOfUrlSpan);

            return domainEndIndex == 0 || IsValid(restOfUrlSpan[..domainEndIndex]); // Check for the domain
        }

        /// <summary>
        ///     Whether a media url carries a scheme the media players are allowed to open.
        ///     The native protocol whitelist is the last gate; this stops schemes no
        ///     backend can play anyway - file:, rtsp:, udp:, ftp: - from reaching it.
        /// </summary>
        public static bool HasAllowedMediaScheme(this string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            ReadOnlySpan<char> urlSpan = url.AsSpan();

            return urlSpan.StartsWith(HTTPS_SCHEME.AsSpan(), StringComparison.OrdinalIgnoreCase)
                   || urlSpan.StartsWith(HTTP_SCHEME.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Whether the url uses the plaintext http:// scheme rather than https.
        /// </summary>
        public static bool IsPlaintextHttpUrl(this string url) =>
            !string.IsNullOrEmpty(url) && url.AsSpan().StartsWith(HTTP_SCHEME.AsSpan(), StringComparison.OrdinalIgnoreCase);

        private static bool IsValid(ReadOnlySpan<char> domain) =>
            !domain.IsEmpty && !domain.IsWhiteSpace();
    }
}
