using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    [LogCategory(ReportCategory.PLAYER_AVATAR_EMOTE_COMMAND)]
    public partial class WriteAvatarEmoteCommandSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteAvatarEmoteCommandSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEmoteCommandQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand(PlayerCRDTEntity playerCRDTEntity, AvatarEmoteCommandComponent emoteCommand)
        {
            if (!emoteCommand.IsDirty || emoteCommand.PlayingEmote.IsNullOrEmpty()) return;

            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.IsDirty = true;
                pbComponent.EmoteUrn = data.emoteCommand.PlayingEmote;
                pbComponent.Loop = data.emoteCommand.LoopingEmote;
                pbComponent.Timestamp = data.timestamp;
            }, playerCRDTEntity.CRDTEntity, tickNumber, (emoteCommand, (uint)tickNumber));
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(AvatarEmoteCommandComponent))]
        private void HandleComponentRemoval(in Entity entity, PlayerCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity);
            World.Remove<AvatarEmoteCommandComponent>(entity);
        }
    }
}
