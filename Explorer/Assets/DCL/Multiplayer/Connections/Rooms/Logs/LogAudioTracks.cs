using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogAudioTracks : IAudioTracks
    {
        private const string PREFIX = "LogAudioTracks:";

        private readonly IAudioTracks origin;

        public LogAudioTracks(IAudioTracks origin)
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
    }
}
