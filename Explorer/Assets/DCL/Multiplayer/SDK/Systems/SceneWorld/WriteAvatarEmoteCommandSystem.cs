using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<AvatarEmoteCommandComponent>))]
    [LogCategory(ReportCategory.PLAYER_AVATAR_EMOTE_COMMAND)]
    public partial class WriteAvatarEmoteCommandSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBAvatarEmoteCommand> componentPool;

        public WriteAvatarEmoteCommandSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBAvatarEmoteCommand> componentPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.componentPool = componentPool;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEmoteCommandQuery(World);
            CreateAvatarEmoteCommandQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAvatarEmoteCommand))]
        private void CreateAvatarEmoteCommand(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent, ref AvatarEmoteCommandComponent emoteCommand)
        {
            if (!emoteCommand.IsDirty || emoteCommand.PlayingEmote.IsNullOrEmpty()) return;

            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.IsDirty = true;
                pbComponent.EmoteUrn = data.emoteCommand.PlayingEmote;
                pbComponent.Loop = data.emoteCommand.LoopingEmote;
                pbComponent.Timestamp = data.timestamp;
            }, playerProfileDataComponent.CRDTEntity, tickNumber, (emoteCommand, (uint)tickNumber));

            PBAvatarEmoteCommand pbComponent = componentPool.Get(); // The engine doesn't track components added through the CRDTWriter
            World.Add(entity, pbComponent, playerProfileDataComponent.CRDTEntity);
        }

        [Query]
        [All(typeof(PBAvatarEmoteCommand))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand(ref PlayerProfileDataComponent playerProfileDataComponent, ref AvatarEmoteCommandComponent emoteCommand)
        {
            if (!emoteCommand.IsDirty || emoteCommand.PlayingEmote.IsNullOrEmpty()) return;

            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.IsDirty = true;
                pbComponent.EmoteUrn = data.emoteCommand.PlayingEmote;
                pbComponent.Loop = data.emoteCommand.LoopingEmote;
                pbComponent.Timestamp = data.timestamp;
            }, playerProfileDataComponent.CRDTEntity, tickNumber, (emoteCommand, (uint)tickNumber));
        }

        [Query]
        [All(typeof(PBAvatarEmoteCommand))]
        [None(typeof(AvatarEmoteCommandComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(crdtEntity);
            World.Remove<PBAvatarEmoteCommand, CRDTEntity>(entity);
        }
    }
}
