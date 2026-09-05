using DCL.Diagnostics;
using LiveKit.Rooms;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using RichTypes;
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

        public Weak<T> ActiveStream(StreamKey key)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(ActiveStream)}: called with {key.identity}, {key.sid}");
            var videoStream = origin.ActiveStream(key);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(ActiveStream)}: result {key.identity}, {key.sid} -> {videoStream.Resource.Has};");
            return videoStream;
        }

        public bool Release(StreamKey key)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{prefix}: {nameof(Release)}: called");
            bool result = origin.Release(key);
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

        public void AssignRoom(Room room)
        {
            origin.AssignRoom(room);
        }

        public void Dispose()
        {
            origin.Dispose();
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
