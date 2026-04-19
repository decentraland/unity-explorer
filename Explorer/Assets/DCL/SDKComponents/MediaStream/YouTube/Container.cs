using System;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Media container format reported by YouTube for an individual stream.
    /// </summary>
    public enum Container
    {
        Unknown,
        Mp4,
        WebM,
        Mov,
        Tgpp,
        Mp3,
    }

    public static class ContainerExtensions
    {
        /// <summary>
        ///     Parses an InnerTube <c>mimeType</c> string (e.g. <c>video/mp4; codecs="avc1.640028"</c>) into a <see cref="Container"/>.
        /// </summary>
        public static Container ParseMimeType(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                return Container.Unknown;

            int semi = mimeType.IndexOf(';');
            ReadOnlySpan<char> head = semi >= 0 ? mimeType.AsSpan(0, semi) : mimeType.AsSpan();

            int slash = head.IndexOf('/');
            if (slash < 0) return Container.Unknown;

            ReadOnlySpan<char> sub = head[(slash + 1)..].Trim();

            if (sub.Equals("mp4", StringComparison.OrdinalIgnoreCase)) return Container.Mp4;
            if (sub.Equals("webm", StringComparison.OrdinalIgnoreCase)) return Container.WebM;
            if (sub.Equals("3gpp", StringComparison.OrdinalIgnoreCase)) return Container.Tgpp;
            if (sub.Equals("mov", StringComparison.OrdinalIgnoreCase) || sub.Equals("quicktime", StringComparison.OrdinalIgnoreCase)) return Container.Mov;
            if (sub.Equals("mpeg", StringComparison.OrdinalIgnoreCase) || sub.Equals("mp3", StringComparison.OrdinalIgnoreCase)) return Container.Mp3;

            return Container.Unknown;
        }
    }
}
