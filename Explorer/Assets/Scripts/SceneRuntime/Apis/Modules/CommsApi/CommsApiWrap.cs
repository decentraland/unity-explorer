using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using JetBrains.Annotations;
using LiveKit.Proto;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.CommsApi
{
    public class CommsApiWrap : IJsApiWrapper
    {
        private readonly IRoomHub roomHub;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly GetActiveVideoStreamsResponse emptyResponse = new (new List<GetActiveVideoStreamsResponse.Stream>());

        public CommsApiWrap(IRoomHub roomHub, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            this.roomHub = roomHub;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/CommsApi.js")]
        public object GetActiveVideoStreams()
        {
            async UniTask<GetActiveVideoStreamsResponse> GetActiveVideoStreamsAsync()
            {
                try
                {
                    await UniTask.SwitchToMainThread();

                    var list = new List<GetActiveVideoStreamsResponse.Stream>();
                    var participants = roomHub.SceneRoom().Room().Participants;

                    foreach (string remoteParticipantIdentity in participants.RemoteParticipantIdentities())
                    {
                        var participant = participants.RemoteParticipant(remoteParticipantIdentity);

                        if (participant == null)
                            continue;

                        foreach (var track in participant.Tracks.Values)
                            if (track.Kind == TrackKind.KindVideo)
                                list.Add(new GetActiveVideoStreamsResponse.Stream(remoteParticipantIdentity, track));
                    }

                    return new GetActiveVideoStreamsResponse(list);
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);
                    return emptyResponse;
                }
            }

            return GetActiveVideoStreamsAsync().ContinueWith(JsonUtility.ToJson).ToDisconnectedPromise();
        }

        public void Dispose() { }
    }
}
