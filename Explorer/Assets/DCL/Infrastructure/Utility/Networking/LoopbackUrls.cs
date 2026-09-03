using System;

namespace Utility.Networking
{
    /// <summary>
    ///     Whether a url addresses this machine — the shared test behind every "this endpoint is local, so it
    ///     may skip TLS, a consent prompt or a CDN detour" gate.
    /// </summary>
    /// <remarks>
    ///     The authority is read as a span and compared against the literals a loopback host can be written as,
    ///     rather than parsed into a <see cref="Uri" />, so nothing here allocates. Ending the host exactly
    ///     where RFC 3986 ends it is what rejects the two shapes a remote host uses to look loopback: a longer
    ///     name that merely starts with one (<c>127.0.0.1.example.com</c>) and loopback-as-userinfo
    ///     (<c>127.0.0.1@example.com</c>).
    /// </remarks>
    public static class LoopbackUrls
    {
        private const string HTTP_SCHEME = "http://";
        private const string HTTPS_SCHEME = "https://";
        private const string WS_SCHEME = "ws://";

        /// <summary>
        ///     True when <paramref name="url" /> is an absolute <c>http://</c> url with a loopback host. For
        ///     gates where cleartext is the point of the check; <see cref="IsLoopbackWebUrl" /> otherwise.
        /// </summary>
        public static bool IsLoopbackHttpUrl(ReadOnlySpan<char> url) =>
            IsLoopbackUrlOf(url, HTTP_SCHEME);

        /// <summary>True when <paramref name="url" /> is an absolute <c>ws://</c> url with a loopback host.</summary>
        public static bool IsLoopbackWsUrl(ReadOnlySpan<char> url) =>
            IsLoopbackUrlOf(url, WS_SCHEME);

        /// <summary>True when <paramref name="url" /> is an absolute <c>http(s)://</c> url with a loopback host.</summary>
        public static bool IsLoopbackWebUrl(ReadOnlySpan<char> url) =>
            IsLoopbackUrlOf(url, HTTP_SCHEME) || IsLoopbackUrlOf(url, HTTPS_SCHEME);

        /// <summary>
        ///     True for the host literals naming the loopback interface. IPv6 arrives bracketed both from a url
        ///     authority and from <see cref="Uri.Host" />, so that is the form accepted here. Deliberately
        ///     literal: the rest of 127.0.0.0/8 has no caller, and resolving a name would turn every gate
        ///     built on this into a DNS-rebinding target.
        /// </summary>
        public static bool IsLoopbackHost(ReadOnlySpan<char> host) =>
            host.Equals("localhost".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1".AsSpan(), StringComparison.Ordinal)
            || host.Equals("[::1]".AsSpan(), StringComparison.Ordinal);

        private static bool IsLoopbackUrlOf(ReadOnlySpan<char> url, string scheme)
        {
            if (!url.StartsWith(scheme.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return false;

            ReadOnlySpan<char> authority = url.Slice(scheme.Length);
            int authorityEnd = authority.IndexOfAny('/', '?', '#');

            if (authorityEnd >= 0)
                authority = authority.Slice(0, authorityEnd);

            return IsLoopbackHost(HostOf(authority));
        }

        /// <summary>
        ///     An authority without the userinfo before the host and the port after it. Reading past the last
        ///     '@' is what keeps a loopback userinfo from passing for the host.
        /// </summary>
        private static ReadOnlySpan<char> HostOf(ReadOnlySpan<char> authority)
        {
            int userInfoEnd = authority.LastIndexOf('@');

            if (userInfoEnd >= 0)
                authority = authority.Slice(userInfoEnd + 1);

            // A bracketed IPv6 host holds the ':' a port would otherwise be found by, so it ends at its ']'.
            if (authority.Length > 0 && authority[0] == '[')
            {
                int bracketEnd = authority.IndexOf(']');
                return bracketEnd < 0 ? authority : authority.Slice(0, bracketEnd + 1);
            }

            int portStart = authority.IndexOf(':');
            return portStart < 0 ? authority : authority.Slice(0, portStart);
        }
    }
}
