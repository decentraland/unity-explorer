using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace DCL.SDKComponents.MediaStream
{
    public static class GoogleDriveMediaExtensions
    {
        private static readonly Regex FILE_ID_REGEX = new (@"\/file\/d\/([\w-]+)", RegexOptions.Compiled);
        private static readonly Regex OPEN_ID_REGEX = new (@"[?&]id=([\w-]+)", RegexOptions.Compiled);

        [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
        public static bool IsGoogleDriveUrl(this string url) =>
            url.IndexOf("drive.google.com/") >= 0 ||
            url.IndexOf("docs.google.com/") >= 0;

        /// <summary>
        ///     Rewrites a Google Drive sharing URL into a direct download URL that AVPro can stream.
        ///     Returns null if the file ID cannot be extracted.
        /// </summary>
        public static string ResolveGoogleDriveDirectUrl(this string url)
        {
            string fileId = ExtractFileId(url);
            return fileId != null
                ? $"https://drive.usercontent.google.com/download?id={fileId}&export=view"
                : null;
        }

        private static string ExtractFileId(string url)
        {
            var match = FILE_ID_REGEX.Match(url);
            if (match.Success)
                return match.Groups[1].Value;

            match = OPEN_ID_REGEX.Match(url);
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }
    }
}
