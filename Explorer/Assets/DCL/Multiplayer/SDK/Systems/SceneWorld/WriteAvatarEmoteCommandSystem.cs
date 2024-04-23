using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [UpdateAfter(typeof(PlayerComponentsHandlerSystem))]
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
        [None(typeof(PBAvatarEmoteCommand), typeof(DeleteEntityIntention))]
        private void CreateAvatarEmoteCommand(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            if (!playerProfileDataComponent.IsPlayingEmoteDirty) return;

            PBAvatarEmoteCommand pbComponent = componentPool.Get();
            var tickNumber = (int)sceneStateProvider.TickNumber;
            pbComponent.IsDirty = true;
            pbComponent.EmoteUrn = playerProfileDataComponent.PlayingEmote;
            pbComponent.Loop = playerProfileDataComponent.LoopingEmote;
            pbComponent.Timestamp = (uint)tickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, PBAvatarEmoteCommand>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.IsDirty = true;
                dispatchedPBComponent.EmoteUrn = pbComponent.EmoteUrn;
                dispatchedPBComponent.Loop = pbComponent.Loop;
                dispatchedPBComponent.Timestamp = pbComponent.Timestamp;
            }, playerProfileDataComponent.CRDTEntity, (int)sceneStateProvider.TickNumber, pbComponent);

            World.Add(entity, pbComponent, playerProfileDataComponent.CRDTEntity);

            playerProfileDataComponent.IsPlayingEmoteDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand(ref PlayerProfileDataComponent playerProfileDataComponent, ref PBAvatarEmoteCommand pbComponent)
        {
            if (!playerProfileDataComponent.IsPlayingEmoteDirty) return;

            var tickNumber = (int)sceneStateProvider.TickNumber;
            pbComponent.IsDirty = true;
            pbComponent.EmoteUrn = playerProfileDataComponent.PlayingEmote;
            pbComponent.Loop = playerProfileDataComponent.LoopingEmote;
            pbComponent.Timestamp = (uint)tickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, PBAvatarEmoteCommand>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.IsDirty = true;
                dispatchedPBComponent.EmoteUrn = pbComponent.EmoteUrn;
                dispatchedPBComponent.Loop = pbComponent.Loop;
                dispatchedPBComponent.Timestamp = pbComponent.Timestamp;
            }, playerProfileDataComponent.CRDTEntity, tickNumber, pbComponent);

            playerProfileDataComponent.IsPlayingEmoteDirty = false;
        }

        [Query]
        [All(typeof(PBAvatarEmoteCommand))]
        [None(typeof(PlayerProfileDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(crdtEntity);
            World.Remove<PBAvatarEmoteCommand, CRDTEntity>(entity);
        }
    }
}
