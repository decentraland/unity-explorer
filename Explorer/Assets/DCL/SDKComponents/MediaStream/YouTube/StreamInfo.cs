namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     A muxed stream — single file containing both video and audio tracks.
    /// </summary>
    public sealed class MuxedStreamInfo : IVideoStreamInfo
    {
        public Container Container { get; }
        public Bitrate Bitrate { get; }
        public string Url { get; }
        public VideoResolution VideoResolution { get; }

        public MuxedStreamInfo(Container container, Bitrate bitrate, string url, VideoResolution videoResolution)
        {
            Container = container;
            Bitrate = bitrate;
            Url = url;
            VideoResolution = videoResolution;
        }
    }

    /// <summary>
    ///     A video-only adaptive stream (no audio track).
    /// </summary>
    public sealed class VideoOnlyStreamInfo : IVideoStreamInfo
    {
        public Container Container { get; }
        public Bitrate Bitrate { get; }
        public string Url { get; }
        public VideoResolution VideoResolution { get; }

        public VideoOnlyStreamInfo(Container container, Bitrate bitrate, string url, VideoResolution videoResolution)
        {
            Container = container;
            Bitrate = bitrate;
            Url = url;
            VideoResolution = videoResolution;
        }
    }
}
