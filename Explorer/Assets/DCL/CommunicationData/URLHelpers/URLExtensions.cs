using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     Contains shortcuts to manipulate with URLs
    /// </summary>
    public static class URLExtensions
    {
        public static URLAddress Append(in this URLDomain address, in URLPath path) =>
            URLBuilder.Combine(address, path);

        public static URLDomain Append(in this URLDomain address, in URLSubdirectory subdirectory) =>
            URLBuilder.Combine(address, subdirectory);

        public static ReadOnlySpan<char> GetBaseDomain(this URLAddress url)
        {
            ReadOnlySpan<char> urlSpan = url.Value.AsSpan();

            // Find the start of the domain part (after "://")
            int protocolEndIndex = urlSpan.IndexOf("://".AsSpan());
            if (protocolEndIndex == -1) return ReadOnlySpan<char>.Empty; // Invalid URL
            protocolEndIndex += 3; // Move past "://"

            // Find the end of the domain part (up to the first '/', if present)
            int pathStartIndex = urlSpan.Slice(protocolEndIndex).IndexOf('/');
            if (pathStartIndex == -1) pathStartIndex = urlSpan.Length - protocolEndIndex; // No path, domain ends at end of URL

            ReadOnlySpan<char> domainSpan = urlSpan.Slice(protocolEndIndex, pathStartIndex);

            // Find the last two parts of the domain (e.g., "example.com")
            int lastDot = domainSpan.LastIndexOf('.');
            if (lastDot == -1) return ReadOnlySpan<char>.Empty; // Invalid domain

            int secondLastDot = domainSpan.Slice(0, lastDot).LastIndexOf('.');
            if (secondLastDot == -1) return domainSpan; // No subdomain, return full domain

            // Return base domain ("example.com")
            return domainSpan.Slice(secondLastDot + 1);
        }
    }
}
