// ReSharper disable once CheckNamespace
namespace DCL.Browser.DecentralandUrls
{
    /// <summary>
    ///     Tags an outgoing Decentraland web URL as coming from the client, so the web side can attribute the visit.
    ///     <para>
    ///         Without this the client is invisible as a traffic source. A native app opening the browser sends no
    ///         Referer header, so every visit the client sends lands in the web analytics as "direct" — the same
    ///         bucket as someone typing the address or using a bookmark. Measured on the Shop over its first days
    ///         live, that bucket was three quarters of all visitors, which makes the question "how much traffic does
    ///         the client actually drive" unanswerable rather than merely imprecise.
    ///     </para>
    ///     <para>
    ///         Only <c>utm_source</c> is added. The other UTM fields carry nothing the destination cannot already
    ///         infer (the campaign is the path, the medium is always organic), and the full set is not free: the
    ///         confirmation dialog shown before leaving for the browser displays the URL in full, so every extra
    ///         parameter is another line of query string in front of the user.
    ///     </para>
    /// </summary>
    public static class ClientSourceUrlExtensions
    {
        private const string SOURCE_PARAM = "utm_source=client";

        /// <summary>
        ///     Returns <paramref name="url" /> with <c>utm_source=client</c> appended, picking <c>?</c> or <c>&amp;</c>
        ///     according to whether the URL already carries a query string. A null or empty URL is returned unchanged:
        ///     callers build these from user data that can legitimately resolve to nothing, and a bare
        ///     <c>?utm_source=client</c> is worse than an empty string because it looks like a link.
        /// </summary>
        public static string WithClientSource(this string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            char separator = url.Contains('?') ? '&' : '?';
            return $"{url}{separator}{SOURCE_PARAM}";
        }
    }
}
