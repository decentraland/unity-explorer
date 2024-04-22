using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
        private void CreateAvatarEmoteCommand(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            if (playerSDKDataComponent.PlayingEmote == null) return;

            PBAvatarEmoteCommand pbComponent = componentPool.Get();
            pbComponent.IsDirty = true;
            pbComponent.EmoteUrn = playerSDKDataComponent.PlayingEmote;
            pbComponent.Loop = playerSDKDataComponent.LoopingEmote;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, PBAvatarEmoteCommand>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.IsDirty = true;
                dispatchedPBComponent.EmoteUrn = pbComponent.EmoteUrn;
                dispatchedPBComponent.Loop = pbComponent.Loop;
            }, playerSDKDataComponent.CRDTEntity, (int)sceneStateProvider.TickNumber, pbComponent);

            World.Add(entity, pbComponent, playerSDKDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent, ref PBAvatarEmoteCommand pbComponent)
        {
            if (!playerSDKDataComponent.IsDirty || playerSDKDataComponent.PlayingEmote == null) return;

            pbComponent.IsDirty = true;
            pbComponent.EmoteUrn = playerSDKDataComponent.PlayingEmote;
            pbComponent.Loop = playerSDKDataComponent.LoopingEmote;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, PBAvatarEmoteCommand>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.IsDirty = true;
                dispatchedPBComponent.EmoteUrn = pbComponent.EmoteUrn;
                dispatchedPBComponent.Loop = pbComponent.Loop;
            }, playerSDKDataComponent.CRDTEntity, (int)sceneStateProvider.TickNumber, pbComponent);

            World.Set(entity, pbComponent);
        }

        [Query]
        [All(typeof(PBAvatarEmoteCommand))]
        [None(typeof(PlayerSDKDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBAvatarEmoteCommand, CRDTEntity>(entity);
        }
    }
}
