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

        public static ReadOnlyMemory<char> GetBaseDomain(this URLAddress url)
        {
            // Find the start of the domain part (after "://")
            ReadOnlySpan<char> urlSpan = url.Value.AsSpan();
            int protocolEndIndex = urlSpan.IndexOf("://".AsSpan());
            if (protocolEndIndex == -1) return ReadOnlyMemory<char>.Empty; // Invalid URL
            protocolEndIndex += 3; // Move past "://"

            // Find the end of the domain part (up to the first '/', if present)
            int pathStartIndex = urlSpan.Slice(protocolEndIndex).IndexOf('/');
            if (pathStartIndex == -1) pathStartIndex = url.Value.Length - protocolEndIndex; // No path, domain ends at end of URL

            ReadOnlyMemory<char> domainMemory = url.Value.AsMemory().Slice(protocolEndIndex, pathStartIndex);

            // Find the last two parts of the domain (e.g., "example.com")
            ReadOnlySpan<char> domainSpan = domainMemory.Span;
            int lastDot = domainSpan.LastIndexOf('.');
            if (lastDot == -1) return ReadOnlyMemory<char>.Empty; // Invalid domain

            int secondLastDot = domainSpan.Slice(0, lastDot).LastIndexOf('.');
            if (secondLastDot == -1) return domainMemory; // No subdomain, return full domain

            // Return base domain ("example.com")
            return domainMemory.Slice(secondLastDot + 1);
        }
    }
}
