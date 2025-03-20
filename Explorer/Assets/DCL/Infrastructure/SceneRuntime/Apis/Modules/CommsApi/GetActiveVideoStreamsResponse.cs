using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using Newtonsoft.Json;
using System;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public static class GetActiveVideoStreamsResponse
    {
        public static void WriteTo(JsonWriter writer, string identity, TrackPublication publication)
        {
            var sourceType = publication.Source switch
                             {
                                 TrackSource.SourceUnknown => VideoTrackSourceType.VTST_UNKNOWN,
                                 TrackSource.SourceCamera => VideoTrackSourceType.VTST_CAMERA,
                                 TrackSource.SourceMicrophone => VideoTrackSourceType.VTST_UNKNOWN,
                                 TrackSource.SourceScreenshare => VideoTrackSourceType.VTST_SCREEN_SHARE,
                                 TrackSource.SourceScreenshareAudio => VideoTrackSourceType.VTST_SCREEN_SHARE,
                                 _ => throw new ArgumentOutOfRangeException()
                             };

            writer.WriteStartObject();
            writer.WritePropertyName("identity");
            writer.WriteValue(identity);
            writer.WritePropertyName("trackSid");

            // like in unity-renderer version trackSid: `livekit-video://${sid}/${videoSid}`,
            // https://github.com/decentraland/unity-renderer/blob/ae68fec703f3c0ebd2251ce7cff2ad465f6f7f7d/browser-interface/packages/shared/apis/host/CommsAPI.ts#L19
            writer.WriteValue(identity.ToLivekitAddress(publication.Sid));

            writer.WritePropertyName("sourceType");
            writer.WriteValue(sourceType);
            writer.WriteEndObject();
        }

        public enum VideoTrackSourceType
        {
            VTST_UNKNOWN = 0,
            VTST_CAMERA = 1,
            VTST_SCREEN_SHARE = 2,
        }
    }
}
