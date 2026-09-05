using System.Collections.Generic;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Container for the muxed and video-only streams returned by a single InnerTube player call.
    /// </summary>
    public sealed class StreamManifest
    {
        private readonly IReadOnlyList<IStreamInfo> muxedStreams;
        private readonly IReadOnlyList<IStreamInfo> videoOnlyStreams;

        public StreamManifest(IReadOnlyList<IStreamInfo> muxedStreams, IReadOnlyList<IStreamInfo> videoOnlyStreams)
        {
            this.muxedStreams = muxedStreams;
            this.videoOnlyStreams = videoOnlyStreams;
        }

        public IEnumerable<IStreamInfo> GetMuxedStreams() => muxedStreams;

        public IEnumerable<IStreamInfo> GetVideoOnlyStreams() => videoOnlyStreams;
    }
}
