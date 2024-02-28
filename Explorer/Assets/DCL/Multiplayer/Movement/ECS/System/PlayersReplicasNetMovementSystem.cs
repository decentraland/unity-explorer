using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PlayersReplicasNetMovementSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly ReplicasMovementInbox inbox;

        public PlayersReplicasNetMovementSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings) : base(world)
        {
            this.settings = settings;
            inbox = new ReplicasMovementInbox(room, settings);

            inbox.InitializeAsync().Forget();
        }

        protected override void Update(float t)
        {
            settings.InboxCount = inbox.Count;

            if (inbox.Count > 0)
            {
                MessageMock message = inbox.Dequeue();
                Debug.Log($"VVV Received {UnityEngine.Time.unscaledTime}");
            }
        }
    }
}
