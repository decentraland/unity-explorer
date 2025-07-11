using System;

namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     Contains shortcuts to manipulate with URLs
    /// </summary>
    public static class URLExtensions
    {
        public static Uri Append(in this URLDomain address, in URLPath path) =>
            URLBuilder.Combine(address, path);

        public static URLDomain Append(in this URLDomain address, in URLSubdirectory subdirectory) =>
            URLBuilder.Combine(address, subdirectory);

        public static Uri? ToURL(this string? url) =>
            url == null ? null :
            Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri uri) ? uri : null;

        /// <summary>
        ///     This method allocates heavily so the result must be cached
        /// </summary>
        public static Uri Append(this Uri uri, string subdirectory)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (string.IsNullOrWhiteSpace(subdirectory))
                return uri;

            if (subdirectory.StartsWith("/"))
                subdirectory = subdirectory[1..];

            return new Uri($"{uri.OriginalString.TrimEnd('/')}/{subdirectory}");
        }
    }
}
