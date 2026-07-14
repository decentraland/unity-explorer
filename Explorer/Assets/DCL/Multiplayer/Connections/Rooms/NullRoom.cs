using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;

#if !UNITY_WEBGL || UNITY_EDITOR
using LiveKit.Rooms.Streaming.Audio;
#endif

using LiveKit.Rooms.Tracks;
using LiveKit.Rooms.Tracks.Hub;
using LiveKit.Rooms.VideoStreaming;
using System.Threading;
using Cysharp.Threading.Tasks;
using RichTypes;
using System;

namespace DCL.Multiplayer.Connections.Rooms
{
    public class NullRoom : IRoom
    {
        public static readonly NullRoom INSTANCE = new ();
        public static readonly WeakReference<IRoom> WEAK_INSTANCE = new (INSTANCE);

        public IActiveSpeakers ActiveSpeakers => NullActiveSpeakers.INSTANCE;
        public IParticipantsHub Participants => NullParticipantsHub.INSTANCE;
        public IDataPipe DataPipe => NullDataPipe.INSTANCE;
        public IRoomInfo Info => NullRoomInfo.INSTANCE;

#if !UNITY_WEBGL || UNITY_EDITOR
        public IVideoStreams VideoStreams => NullVideoStreams.INSTANCE;
        public IAudioStreams AudioStreams => NullAudioStreams.INSTANCE;
        public ILocalTracks LocalTracks => NullLocalTracks.INSTANCE;

        public event LocalPublishDelegate? LocalTrackPublished { add { } remove { } }
        public event LocalPublishDelegate? LocalTrackUnpublished { add { } remove { } }
        public event PublishDelegate? TrackPublished { add { } remove { } }
        public event PublishDelegate? TrackUnpublished { add { } remove { } }
        public event SubscribeDelegate? TrackSubscribed { add { } remove { } }
        public event SubscribeDelegate? TrackUnsubscribed { add { } remove { } }
        public event MuteDelegate? TrackMuted { add { } remove { } }
        public event MuteDelegate? TrackUnmuted { add { } remove { } }
#endif

        public event ConnectionQualityChangeDelegate? ConnectionQualityChanged { add { } remove { } }
        public event ConnectionStateChangeDelegate? ConnectionStateChanged { add { } remove { } }
        public event ConnectionDelegate? ConnectionUpdated { add { } remove { } }
        public event Room.MetaDelegate? RoomMetadataChanged { add { } remove { } }
        public event Room.SidDelegate? RoomSidChanged { add { } remove { } }

        public void UpdateLocalMetadata(string metadata)
        {
            //ignore
        }

        public void SetLocalName(string name) { }

        public UniTask<Result> ConnectAsync(string url, string authToken, CancellationToken cancelToken, bool autoSubscribe) =>
            UniTask.FromResult(Result.SuccessResult());

        public UniTask DisconnectAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
