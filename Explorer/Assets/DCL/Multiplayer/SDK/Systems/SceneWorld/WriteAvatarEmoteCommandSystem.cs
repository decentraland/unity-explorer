using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WriteAvatarEmoteCommandSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteAvatarEmoteCommandSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        public override void Initialize()
        {
            UpdateAvatarEmoteCommandQuery(World, true);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEmoteCommandQuery(World, false);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand([Data] bool force, PlayerSceneCRDTEntity crdtEntity, AvatarEmoteCommandComponent emoteCommand)
        {
            if ((!force && !emoteCommand.IsDirty) || emoteCommand.PlayingEmote.IsNullOrEmpty()) return;

            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.IsDirty = true;
                pbComponent.EmoteUrn = data.emoteCommand.PlayingEmote;
                pbComponent.Loop = data.emoteCommand.LoopingEmote;
                pbComponent.Timestamp = data.timestamp;
            }, crdtEntity.CRDTEntity, tickNumber, (emoteCommand, (uint)tickNumber));
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(AvatarEmoteCommandComponent))]
        private void HandleComponentRemoval(in Entity entity, PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity);
            World.Remove<AvatarEmoteCommandComponent>(entity);
        }
    }
}
