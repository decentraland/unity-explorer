using DCL.Diagnostics;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogStreams<T> : IStreams<T> where T: class
    {
        private readonly IStreams<T> origin;
        private readonly string prefix;

        protected LogStreams(IStreams<T> origin, string prefix)
        {
            this.origin = origin;
            this.prefix = prefix;
        }

        public WeakReference<T>? ActiveStream(string identity, string sid)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(ActiveStream)}: called with {identity}, {sid}");
            var videoStream = origin.ActiveStream(identity, sid);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(ActiveStream)}: result {identity}, {sid} -> {videoStream?.TryGetTarget(out _)};");
            return videoStream;
        }

        public bool Release(T stream)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(Release)}: called");
            bool result = origin.Release(stream);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(Release)}: done");
            return result;
        }

        public void Free()
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(Free)}: called");
            origin.Free();
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(Free)}: done");
        }
    }

    public class LogVideoStreams : LogStreams<IVideoStream>, IVideoStreams
    {
        public LogVideoStreams(IStreams<IVideoStream> origin) : base(origin, nameof(LogVideoStreams)) { }
    }

    public class LogAudioStreams : LogStreams<IAudioStream>, IAudioStreams
    {
        public LogAudioStreams(IStreams<IAudioStream> origin) : base(origin, nameof(LogVideoStreams)) { }
    }
}
