using System;
using System.Text.RegularExpressions;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Strongly-typed YouTube video identifier (11-character base64url token).
    /// </summary>
    public readonly struct VideoId : IEquatable<VideoId>
    {
        private const int LENGTH = 11;

        // YouTube IDs are exactly 11 chars from the URL-safe base64 alphabet.
        private static readonly Regex BARE_ID = new (@"^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

        // ?v= or &v= followed by 11 chars
        private static readonly Regex WATCH_PARAM = new (@"[?&]v=([A-Za-z0-9_-]{11})", RegexOptions.Compiled);

        // youtu.be/<id>, /live/<id>, /shorts/<id>, /embed/<id>, /v/<id>
        private static readonly Regex PATH_SEGMENT = new (
            @"(?:youtu\.be/|/(?:live|shorts|embed|v)/)([A-Za-z0-9_-]{11})",
            RegexOptions.Compiled);

        public string Value { get; }

        private VideoId(string value)
        {
            Value = value;
        }

        public static VideoId? TryParse(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            if (BARE_ID.IsMatch(input))
                return new VideoId(input);

            Match match = PATH_SEGMENT.Match(input);

            if (match.Success)
                return new VideoId(match.Groups[1].Value);

            match = WATCH_PARAM.Match(input);

            if (match.Success)
                return new VideoId(match.Groups[1].Value);

            return null;
        }

        public bool Equals(VideoId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is VideoId other && Equals(other);

        public override int GetHashCode() =>
            Value?.GetHashCode() ?? 0;

        public override string ToString() =>
            Value ?? string.Empty;

        public static implicit operator string(VideoId id) =>
            id.Value;
    }
}
