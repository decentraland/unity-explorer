using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;

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

        public ITrack CreateAudioTrack(string name, RtcAudioSource source, IRoom room)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: create Audio Track with name {name}");
            var audioTrack = origin.CreateAudioTrack(name, source, room);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{PREFIX}: created Audio Track with name {name} with SID: {audioTrack.Sid}");
            return audioTrack;
        }
    }
}
