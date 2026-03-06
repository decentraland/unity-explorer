using DCL.Multiplayer.Connections.Rooms;
using DCL.SDKComponents.MediaStream;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public static class GetActiveVideoStreamsResponse
    {
        private static VideoTrackSourceType From(TrackSource trackSource) =>
            trackSource switch
            {
                TrackSource.SourceUnknown => VideoTrackSourceType.VTST_UNKNOWN,
                TrackSource.SourceCamera => VideoTrackSourceType.VTST_CAMERA,
                TrackSource.SourceMicrophone => VideoTrackSourceType.VTST_UNKNOWN,
                TrackSource.SourceScreenshare => VideoTrackSourceType.VTST_SCREEN_SHARE,
                TrackSource.SourceScreenshareAudio => VideoTrackSourceType.VTST_SCREEN_SHARE,
                _ => throw new ArgumentOutOfRangeException()
            };

        private static string ResolveDisplayName(Participant participant)
        {
            string metadata = participant.Metadata;

            if (!string.IsNullOrEmpty(metadata))
            {
                try
                {
                    var json = JObject.Parse(metadata);
                    string displayName = json.Value<string>("displayName");

                    if (!string.IsNullOrEmpty(displayName))
                        return displayName;
                }
                catch (JsonException) { }
            }

            return !string.IsNullOrEmpty(participant.Name) ? participant.Name : participant.Identity;
        }

        private static void WriteTo(JsonWriter writer, string identity, string trackSid, Participant participant, TrackPublication publication)
        {
            var sourceType = From(publication.Source);

            writer.WriteStartObject();
            writer.WritePropertyName("identity");
            writer.WriteValue(identity);
            writer.WritePropertyName("trackSid");
            writer.WriteValue(trackSid);
            writer.WritePropertyName("sourceType");
            writer.WriteValue(sourceType);
            writer.WritePropertyName("name");
            writer.WriteValue(ResolveDisplayName(participant));
            writer.WritePropertyName("speaking");
            writer.WriteValue(participant.Speaking);
            writer.WritePropertyName("trackName");
            writer.WriteValue(publication.Name);
            writer.WritePropertyName("width");
            writer.WriteValue(publication.Width);
            writer.WritePropertyName("height");
            writer.WriteValue(publication.Height);
            writer.WriteEndObject();
        }

        public static void WriteTo(JsonWriter writer, string identity, Participant participant, TrackPublication publication)
        {
            // like in unity-renderer version trackSid: `livekit-video://${sid}/${videoSid}`,
            // https://github.com/decentraland/unity-renderer/blob/ae68fec703f3c0ebd2251ce7cff2ad465f6f7f7d/browser-interface/packages/shared/apis/host/CommsAPI.ts#L19
            string address = identity.ToLivekitAddress(publication.Sid);
            WriteTo(writer, identity, address, participant, publication);
        }

        public static void WriteAsCurrentTo(JsonWriter writer, string identity, Participant participant, TrackPublication publication) =>
            WriteTo(writer, identity, LiveKitMediaExtensions.LIVEKIT_CURRENT_STREAM, participant, publication);

        public enum VideoTrackSourceType
        {
            VTST_UNKNOWN = 0,
            VTST_CAMERA = 1,
            VTST_SCREEN_SHARE = 2,
        }
    }
}
