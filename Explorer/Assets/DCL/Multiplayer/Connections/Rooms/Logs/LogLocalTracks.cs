#if !UNITY_WEBGL

using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogLocalTracks : ILocalTracks
    {
        private const string PREFIX = "LogAudioTracks:";

        private readonly ILocalTracks origin;

        public LogLocalTracks(ILocalTracks origin)
        {
            this.origin = origin;
        }

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: create Audio Track with name {name}");
            var audioTrack = origin.CreateAudioTrack(name, source);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: created Audio Track with name {name} and SID: {audioTrack.Sid}");
            return audioTrack;
        }

        public ITrack CreateVideoTrack(string name, RtcVideoSource source)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: create Video Track with name {name}");
            var track = origin.CreateVideoTrack(name, source);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: created Video Track with name {name} and SID: {track.Sid}");
            return track;
        }
    }
}

#endif
