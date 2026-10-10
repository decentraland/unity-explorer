using System.Diagnostics.CodeAnalysis;

namespace DCL.SDKComponents.MediaStream
{
    public static class YouTubeMediaExtensions
    {
        [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
        public static bool IsYouTubeUrl(this string url) =>
            url.IndexOf("youtube.com/watch") >= 0 ||
            url.IndexOf("youtu.be/") >= 0 ||
            url.IndexOf("youtube.com/live/") >= 0 ||
            url.IndexOf("youtube.com/shorts/") >= 0;
    }
}
