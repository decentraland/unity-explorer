namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Metadata for a single stream entry returned by YouTube's player endpoint.
    /// </summary>
    public interface IStreamInfo
    {
        Container Container { get; }
        Bitrate Bitrate { get; }
        string Url { get; }
    }

    /// <summary>
    ///     A stream that carries video (muxed video+audio or video-only).
    /// </summary>
    public interface IVideoStreamInfo : IStreamInfo
    {
        VideoResolution VideoResolution { get; }
    }
}
