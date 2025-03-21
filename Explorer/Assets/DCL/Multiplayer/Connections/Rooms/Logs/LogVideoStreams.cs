using DCL.Diagnostics;
using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogVideoStreams : IVideoStreams
    {
        private readonly IVideoStreams origin;

        public LogVideoStreams(IVideoStreams origin)
        {
            this.origin = origin;
        }

        public WeakReference<IVideoStream>? VideoStream(string identity, string sid)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(VideoStream)}: called with {identity}, {sid}");
            var videoStream = origin.VideoStream(identity, sid);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(VideoStream)}: result {identity}, {sid} -> {videoStream?.TryGetTarget(out _ )};");
            return videoStream;
        }

        public bool Release(IVideoStream videoStream)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(Release)}: called");
            bool result = origin.Release(videoStream);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(Release)}: done");
            return result;
        }

        public void Free()
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(Free)}: called");
            origin.Free();
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(LogVideoStreams)}: {nameof(Free)}: done");
        }
    }
}
