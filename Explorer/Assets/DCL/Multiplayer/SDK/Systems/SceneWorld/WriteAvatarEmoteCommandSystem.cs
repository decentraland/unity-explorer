using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.Emotes;
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
            ForceReplayPlayingEmoteQuery(World);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarEmoteCommandQuery(World);
        }

        /// <summary>
        ///     Replays only the started state of an emote that is still playing when the system initializes.
        ///     Stop events are never replayed: they are one-shot transitions, not state.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ForceReplayPlayingEmote(in PlayerSceneCRDTEntity crdtEntity, in AvatarEmoteCommandComponent emoteCommand)
        {
            if (!emoteCommand.IsPlaying || emoteCommand.PlayingEmote.IsNullOrEmpty()) return;

            AppendStart(crdtEntity.CRDTEntity, emoteCommand.PlayingEmote, emoteCommand.LoopingEmote);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarEmoteCommand(in PlayerSceneCRDTEntity crdtEntity, ref AvatarEmoteCommandComponent emoteCommand)
        {
            if (!emoteCommand.IsDirty) return;

            // The stop always belongs to the previous playback and the start to the new one, so the stop is
            // appended first. Both entries share the same tick timestamp: the GOVS retains both, in insertion order.
            if (emoteCommand.StopEvent.IsSet)
                AppendStop(crdtEntity.CRDTEntity, emoteCommand.StopEvent);

            if (emoteCommand.StartEvent.IsSet)
                AppendStart(crdtEntity.CRDTEntity, emoteCommand.StartEvent.Urn, emoteCommand.StartEvent.Loop);

            // One-shot events are consumed here; the replay snapshot fields stay untouched.
            emoteCommand.StartEvent = default(EmoteStartEvent);
            emoteCommand.StopEvent = default(EmoteStopEvent);
            emoteCommand.IsDirty = false;
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(AvatarEmoteCommandComponent))]
        private void HandleComponentRemoval(in Entity entity, PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity);
            World.Remove<AvatarEmoteCommandComponent>(entity);
        }

        private void AppendStart(CRDTEntity crdtEntity, URN urn, bool loop)
        {
            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (URN urn, bool loop, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.EmoteUrn = data.urn;
                pbComponent.Loop = data.loop;
                pbComponent.Timestamp = data.timestamp;
                pbComponent.State = EmoteState.EsStarted;
            }, crdtEntity, tickNumber, (urn, loop, (uint)tickNumber));
        }

        private void AppendStop(CRDTEntity crdtEntity, in EmoteStopEvent stopEvent)
        {
            var tickNumber = (int)sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage<PBAvatarEmoteCommand, (EmoteStopEvent stopEvent, uint timestamp)>(static (pbComponent, data) =>
            {
                pbComponent.EmoteUrn = data.stopEvent.Urn;
                pbComponent.Loop = data.stopEvent.Loop;
                pbComponent.Timestamp = data.timestamp;
                pbComponent.State = data.stopEvent.Reason;
            }, crdtEntity, tickNumber, (stopEvent, (uint)tickNumber));
        }
    }
}
