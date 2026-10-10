using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogLocalTracks: ILocalTracks
    {
        private const string PREFIX = "LogLocalTracks:";

        private readonly ILocalTracks origin;

        public LogLocalTracks(ILocalTracks origin)
        {
            this.origin = origin;
        }

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} {nameof(CreateAudioTrack)} called name:{name} sourceType:{source.GetType().Name}");

            ITrack track = origin.CreateAudioTrack(name, source);

            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} {nameof(CreateAudioTrack)} result trackType:{track.GetType().Name}");

            return track;
        }

        public ITrack CreateVideoTrack(string name, RtcVideoSource source)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} {nameof(CreateVideoTrack)} called name:{name} sourceType:{source.GetType().Name}");

            ITrack track = origin.CreateVideoTrack(name, source);

            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} {nameof(CreateVideoTrack)} result trackType:{track.GetType().Name}");

            return track;
        }
    }
}
