using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class RemoteMetadata : IRemoteMetadata
    {
        private readonly IRoomHub roomHub;
        private readonly ConcurrentDictionary<string, IRemoteMetadata.ParticipantMetadata> metadata = new ();
        private readonly IRealmData realmData;

        private string sceneRoomSId;

        public RemoteMetadata(IRoomHub roomHub, IRealmData realmData)
        {
            this.roomHub = roomHub;
            this.realmData = realmData;

            roomHub.IslandRoom().Participants.UpdatesFromParticipant += OnUpdatesFromParticipantInIsland;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += OnUpdatesFromParticipantInSceneRoom;

            // OnConnected will be called while the room is not assigned, so the callback is missed
            //roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectedToSceneRoom;
        }

        ~RemoteMetadata()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= OnUpdatesFromParticipantInIsland;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant -= OnUpdatesFromParticipantInSceneRoom;

            //roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectedToSceneRoom;
        }

        public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata => metadata;

        private void OnUpdatesFromParticipantInIsland(Participant participant, UpdateFromParticipant update)
        {
            if (update is UpdateFromParticipant.MetadataChanged or UpdateFromParticipant.Connected)
            {
                if (participant.Metadata == null) return;

                IslandMetadata message = JsonUtility.FromJson<IslandMetadata>(participant.Metadata);
                ParticipantsOnUpdatesFromParticipant(participant, new IRemoteMetadata.ParticipantMetadata(new Vector2Int(message.x, message.y), URLDomain.FromString(message.lambdasEndpoint)));
            }
        }

        // private void OnConnectedToSceneRoom(IRoom room, ConnectionUpdate connectionUpdate)
        // {
        //     if (connectionUpdate is ConnectionUpdate.Connected or ConnectionUpdate.Reconnected)
        //     {
        //         // Set metadata once
        //         SendAsync(new SceneRoomMetadata(realmData.Ipfs.LambdasBaseUrl.Value)).Forget();
        //     }
        // }

        private void OnUpdatesFromParticipantInSceneRoom(Participant participant, UpdateFromParticipant update)
        {
            if (update is UpdateFromParticipant.MetadataChanged or UpdateFromParticipant.Connected)
            {
                if (participant.Metadata == null)
                    return;

                IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();
                SceneShortInfo? sceneInfo = sceneRoom.ConnectedScene;
                if (sceneInfo == null) return;

                SceneRoomMetadata message = JsonUtility.FromJson<SceneRoomMetadata>(participant.Metadata);
                ParticipantsOnUpdatesFromParticipant(participant, new IRemoteMetadata.ParticipantMetadata(sceneInfo.Value.BaseParcel, URLDomain.FromString(message.lambdasEndpoint)));
            }
        }

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, IRemoteMetadata.ParticipantMetadata participantMetadata)
        {
            metadata[participant.Identity] = participantMetadata;
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"{nameof(RemoteMetadata)}: metadata of {participant.Identity} is {participantMetadata}");
        }

        public void BroadcastSelfParcel(Vector2Int pose)
        {
            if (!realmData.Configured)
                return;

            // Broadcasting self position makes sense only for the island
            SendAsync(new IslandMetadata(pose.x, pose.y, realmData.Ipfs.LambdasBaseUrl.Value)).Forget();
        }

        public void BroadcastSelfMetadata()
        {
            if (!realmData.Configured)
                return;

            string currentRoomSid = roomHub.SceneRoom().Room().Info.Sid;

            if (sceneRoomSId != currentRoomSid)
            {
                SendAsync(new SceneRoomMetadata(realmData.Ipfs.LambdasBaseUrl.Value)).Forget();
                sceneRoomSId = currentRoomSid;
            }
        }

        private async UniTaskVoid SendAsync(IslandMetadata islandMetadata)
        {
            await UniTask.SwitchToThreadPool();
            roomHub.IslandRoom().UpdateLocalMetadata(islandMetadata.ToJson());
            ReportHub.Log(ReportCategory.MULTIPLAYER, $"{nameof(RemoteMetadata)}: {nameof(IslandMetadata)} {islandMetadata} of self is sent");
        }

        private async UniTaskVoid SendAsync(SceneRoomMetadata sceneRoomMetadata)
        {
            await UniTask.SwitchToThreadPool();
            roomHub.SceneRoom().Room().UpdateLocalMetadata(sceneRoomMetadata.ToJson());
            ReportHub.Log(ReportCategory.MULTIPLAYER, $"{nameof(RemoteMetadata)}: {nameof(SceneRoomMetadata)} {sceneRoomMetadata} of self is sent");
        }

        //TODO later transfer to Proto
        [Serializable]
        public struct IslandMetadata
        {
            public int x;
            public int y;
            public string lambdasEndpoint;

            public IslandMetadata(int x, int y, string lambdasEndpoint)
            {
                this.x = x;
                this.y = y;
                this.lambdasEndpoint = lambdasEndpoint;
            }

            public string ToJson() =>
                JsonUtility.ToJson(this)!;

            public override string ToString() =>
                ToJson();
        }

        [Serializable]
        public struct SceneRoomMetadata
        {
            public string lambdasEndpoint;

            public SceneRoomMetadata(string lambdasEndpoint)
            {
                this.lambdasEndpoint = lambdasEndpoint;
            }

            public string ToJson() =>
                JsonUtility.ToJson(this)!;

            public override string ToString() =>
                ToJson();
        }
    }
}
