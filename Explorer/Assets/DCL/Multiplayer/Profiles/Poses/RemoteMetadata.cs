using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement;
using ECS;
using LiveKit.Rooms.Participants;
using SceneRunner.Scene;
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

        private string previousSceneRoomSId;
        private Vector2Int? previousParcel;

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
                if (string.IsNullOrEmpty(participant.Metadata))
                    return;

                PeerMetadata message;

                try { message = JsonUtility.FromJson<PeerMetadata>(participant.Metadata); }
                catch (Exception) { return; }

                ParticipantsOnUpdatesFromParticipant(participant, message.ToRemoteMetadata());
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
                if (string.IsNullOrEmpty(participant.Metadata))
                    return;

                IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();
                ISceneData? sceneInfo = sceneRoom.ConnectedScene;
                if (sceneInfo == null) return;

                PeerMetadata message;

                try { message = JsonUtility.FromJson<PeerMetadata>(participant.Metadata); }
                catch (Exception) { return; }

                ParticipantsOnUpdatesFromParticipant(participant, message.ToRemoteMetadata());
            }
        }

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, IRemoteMetadata.ParticipantMetadata participantMetadata)
        {
            metadata[participant.Identity] = participantMetadata;
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"{nameof(RemoteMetadata)}: metadata of {participant.Identity} is {participantMetadata}");
        }

        public void Remove(string walletId)
        {
            metadata.Remove(walletId, out _);
        }

        public void BroadcastMetadata(Vector2Int pose)
        {
            if (!realmData.Configured)
                return;

            string currentRoomSid = roomHub.SceneRoom().Room().Info.Sid;
            bool sceneRoomChanged = previousSceneRoomSId != currentRoomSid;
            previousSceneRoomSId = currentRoomSid;

            bool parcelChanged = previousParcel != pose;
            previousParcel = pose;

            var peerMetadata = new PeerMetadata(pose.x, pose.y, realmData.Ipfs.LambdasBaseUrl.Value);

            SendAsync(peerMetadata, sceneRoomChanged, parcelChanged).Forget();
        }

        private async UniTaskVoid SendAsync(PeerMetadata peerMetadata, bool sceneRoomChanged, bool parcelChanged)
        {
            await UniTask.SwitchToThreadPool();

            string encodedMetadata = peerMetadata.ToJson();

            if (parcelChanged || sceneRoomChanged)
                roomHub.SceneRoom().Room().UpdateLocalMetadata(encodedMetadata);

            if (parcelChanged)
                roomHub.IslandRoom().UpdateLocalMetadata(encodedMetadata);

            // Update local metadata immediately (needed for self-replica)
            metadata[RemotePlayerMovementComponent.SELF_REPLICA_ID] = peerMetadata.ToRemoteMetadata();

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"{nameof(RemoteMetadata)}: {nameof(PeerMetadata)} {peerMetadata} of self is sent");
        }

        //TODO later transfer to Proto
        [Serializable]
        public struct PeerMetadata
        {
            // Even for Scene Rooms parcel must be written as well because:
            // 1. There is only one room in realms
            // 2. User can be still connected to the old scene room for a short time
            public int x;
            public int y;
            public string lambdasEndpoint;

            public PeerMetadata(int x, int y, string lambdasEndpoint)
            {
                this.x = x;
                this.y = y;
                this.lambdasEndpoint = lambdasEndpoint;
            }

            public string ToJson() =>
                JsonUtility.ToJson(this)!;

            public override string ToString() =>
                ToJson();

            public IRemoteMetadata.ParticipantMetadata ToRemoteMetadata() =>
                new (new Vector2Int(x, y), URLDomain.FromString(lambdasEndpoint));
        }
    }
}
