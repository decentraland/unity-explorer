using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Rooms.Participants;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public class RemotePoses : IRemotePoses
    {
        private readonly IRoomHub roomHub;
        private readonly ConcurrentDictionary<string, Vector2Int> poses = new ();
        private readonly Action<string> log;

        public int Count => poses.Count;

        public RemotePoses(IRoomHub roomHub) : this(roomHub, ReportHub.WithReport(ReportCategory.MULTIPLAYER_MOVEMENT).Log)
        {
        }

        public RemotePoses(IRoomHub roomHub, Action<string> log)
        {
            this.roomHub = roomHub;
            this.log = log;

            roomHub.IslandRoom().Participants.UpdatesFromParticipant += ParticipantsOnUpdatesFromParticipant;
        }

        public IEnumerator<KeyValuePair<string, Vector2Int>> GetEnumerator() =>
            poses.GetEnumerator();

        ~RemotePoses()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= ParticipantsOnUpdatesFromParticipant;
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            if (update is UpdateFromParticipant.MetadataChanged)
            {
                var message = JsonUtility.FromJson<Message>(participant.Metadata);
                var pose = poses[participant.Identity] = new Vector2Int(message.x, message.y);
                log($"RemotePoses: pose of {participant.Identity} is {pose}");
            }
        }

        public Vector2Int ParcelPose(string walletId)
        {
            poses.TryGetValue(walletId, out var pose);
            return pose;
        }

        public void BroadcastSelfPose(Vector2Int pose)
        {
            SendAsync(pose).Forget();
        }

        private async UniTaskVoid SendAsync(Vector2Int pose)
        {
            await UniTask.SwitchToThreadPool();
            roomHub.IslandRoom().UpdateLocalMetadata(new Message(pose).ToJson());
            log($"RemotePoses: pose of self {pose} is sent");
        }

        //TODO later transfer to Proto

        [Serializable]
        private struct Message
        {
            public int x;
            public int y;

            public Message(Vector2Int vector2Int) : this(vector2Int.x, vector2Int.y) { }

            public Message(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public string ToJson() =>
                JsonUtility.ToJson(this)!;
        }
    }
}
