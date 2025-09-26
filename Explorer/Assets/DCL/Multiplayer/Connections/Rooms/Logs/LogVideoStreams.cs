using DCL.Diagnostics;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogStreams<T, TInfo> : IStreams<T, TInfo> where T: class
    {
        private readonly IStreams<T, TInfo> origin;
        private readonly string prefix;

        protected LogStreams(IStreams<T, TInfo> origin, string prefix)
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

        public void ListInfo(List<StreamInfo<TInfo>> output)
        {
            origin.ListInfo(output);
        }
    }

    public class LogVideoStreams : LogStreams<IVideoStream, VideoStreamInfo>, IVideoStreams
    {
        public LogVideoStreams(IStreams<IVideoStream, VideoStreamInfo> origin) : base(origin, nameof(LogVideoStreams)) { }
    }

    public class LogAudioStreams : LogStreams<AudioStream, AudioStreamInfo>, IAudioStreams
    {
        public LogAudioStreams(IStreams<AudioStream, AudioStreamInfo> origin) : base(origin, nameof(LogVideoStreams)) { }
    }
}
