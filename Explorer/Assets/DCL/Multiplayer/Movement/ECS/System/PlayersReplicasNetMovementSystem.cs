using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PlayersReplicasNetMovementSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom room;
        private readonly IMultiplayerSpatialStateSettings settings;

        private bool isSbuscribed;

        public PlayersReplicasNetMovementSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings) : base(world)
        {
            this.room = room;
            this.settings = settings;
        }

        ~PlayersReplicasNetMovementSystem()
        {
            if (isSbuscribed)
                room.Room().DataPipe.DataReceived -= OnDataReceived;
        }

        private void OnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            Debug.Log("VVV OnDataReceived");
        }

        protected override void Update(float t)
        {
            if (!isSbuscribed && room.IsRunning())
            {
                room.Room().DataPipe.DataReceived += OnDataReceived;
                isSbuscribed = true;
            }
        }
    }
}
