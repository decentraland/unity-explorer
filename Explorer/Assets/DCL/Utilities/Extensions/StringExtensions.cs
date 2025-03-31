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

        private static bool IsValid(ReadOnlySpan<char> domain) =>
            !domain.IsEmpty && !domain.IsWhiteSpace();
    }
}
