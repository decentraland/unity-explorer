using JetBrains.Annotations;
using LiveKit.Proto;
using LiveKit.Rooms.TrackPublications;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    [Serializable]
    [PublicAPI]
    public class GetActiveVideoStreamsResponse
    {
        public List<Stream> streams;

        public GetActiveVideoStreamsResponse(List<Stream> streams)
        {
            this.streams = streams;
        }

        [Serializable]
        [PublicAPI]
        public class Stream
        {
            public string identity;
            public string trackSid;
            public VideoTrackSourceType sourceType;

            public Stream(string identity, TrackPublication publication)
            {
                this.identity = identity;

                // like in unity-renderer version trackSid: `livekit-video://${sid}/${videoSid}`,
                // https://github.com/decentraland/unity-renderer/blob/ae68fec703f3c0ebd2251ce7cff2ad465f6f7f7d/browser-interface/packages/shared/apis/host/CommsAPI.ts#L19
                this.trackSid = $"livekit-video://{identity}/{publication.Sid}";

                sourceType = publication.Source switch
                             {
                                 TrackSource.SourceUnknown => VideoTrackSourceType.VTST_UNKNOWN,
                                 TrackSource.SourceCamera => VideoTrackSourceType.VTST_CAMERA,
                                 TrackSource.SourceMicrophone => VideoTrackSourceType.VTST_UNKNOWN,
                                 TrackSource.SourceScreenshare => VideoTrackSourceType.VTST_SCREEN_SHARE,
                                 TrackSource.SourceScreenshareAudio => VideoTrackSourceType.VTST_SCREEN_SHARE,
                                 _ => throw new ArgumentOutOfRangeException()
                             };
            }

            public enum VideoTrackSourceType
            {
                VTST_UNKNOWN = 0,
                VTST_CAMERA = 1,
                VTST_SCREEN_SHARE = 2,
            }
        }
    }
}
