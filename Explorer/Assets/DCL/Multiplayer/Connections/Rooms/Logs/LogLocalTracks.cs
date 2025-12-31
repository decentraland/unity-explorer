using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogLocalTracks : ILocalTracks
    {
        private readonly ILocalTracks origin;

        public LogLocalTracks(ILocalTracks origin)
        {
            this.origin = origin;
        }

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(CreateAudioTrack)}: called with {name}");
            ITrack track = origin.CreateAudioTrack(name, source);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(CreateAudioTrack)}: finish {name} and sid: {track.Sid}");
            return track;
        }

        public ITrack CreateVideoTrack(string name, RtcVideoSource source)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(CreateVideoTrack)}: called with {name}");
            ITrack track = origin.CreateVideoTrack(name, source);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{nameof(CreateVideoTrack)}: finish {name} and sid: {track.Sid}");
            return track;
        }
    }
}
