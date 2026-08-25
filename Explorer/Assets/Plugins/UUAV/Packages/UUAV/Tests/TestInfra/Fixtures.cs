namespace UUAV.Tests
{
    /// <summary>
    /// Ground truth for the committed media in Tests/Fixtures~. Values mirror
    /// what Fixtures~/generate.sh encodes; regenerate the media and update
    /// both together.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>6 s, 320x240 h264 + 440 Hz sine aac; solid red [0,2)s, green [2,4)s, blue [4,6)s.</summary>
        public const string ToneColorBands = "tone_color_bands.mp4";

        public const string AudioOnly = "audio_only.m4a";
        public const string VideoOnly = "video_only.mp4";

        /// <summary>Deterministic non-media bytes behind an mp4 name.</summary>
        public const string Garbage = "garbage.mp4";

        /// <summary>Valid ftyp with a cut-off moov; probing must fail.</summary>
        public const string Truncated = "truncated.mp4";

        public const double DurationSeconds = 6.0;
        public const int Width = 320;
        public const int Height = 240;
        public const string VideoCodec = "h264";
        public const string AudioCodec = "aac";

        /// <summary>Start of the blue band in <see cref="ToneColorBands"/>.</summary>
        public const double BlueBandStartSeconds = 4.0;
    }
}
