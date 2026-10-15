using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Mirrors Nearby voice activity into <c>PBAvatarBase.talking</c>.
    ///     A remote player's <see cref="IsActivelySpeakingTag" /> transitions (tracked through <see cref="TalkingMirroredTag" />)
    ///     and scene assignments are written to the <see cref="SDKTalking" /> of the scene the player stands in.
    ///     The local player's value is written to <see cref="TalkingComponent" /> on the global player entity.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerProfileDataPropagationSystem))]
    [UpdateAfter(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyTalkingSDKPropagationSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly NearbyVoiceChatStateModel stateModel;

        internal NearbyTalkingSDKPropagationSystem(World world, Entity playerEntity, NearbyVoiceChatStateModel stateModel) : base(world)
        {
            this.playerEntity = playerEntity;
            this.stateModel = stateModel;
        }

        public override void Initialize()
        {
            World.Add(playerEntity, new TalkingComponent());
        }

        protected override void Update(float t)
        {
            World.Get<TalkingComponent>(playerEntity).IsTalking = stateModel.IsLocalSpeaking;
            StartTalkingQuery(World);
            StopTalkingQuery(World);
            SeedOnSceneAssignmentQuery(World);
        }

        [Query]
        [All(typeof(IsActivelySpeakingTag))]
        [None(typeof(TalkingMirroredTag), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void StartTalking(Entity entity, in PlayerCRDTEntity playerCRDTEntity)
        {
            SetSceneTalking(in playerCRDTEntity, true);
            World.Add<TalkingMirroredTag>(entity);
        }

        [Query]
        [All(typeof(TalkingMirroredTag))]
        [None(typeof(IsActivelySpeakingTag), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void StopTalking(Entity entity, in PlayerCRDTEntity playerCRDTEntity)
        {
            SetSceneTalking(in playerCRDTEntity, false);
            World.Remove<TalkingMirroredTag>(entity);
        }

        /// <summary>
        ///     Writes the current state on the frame the scene assignment changes.
        /// </summary>
        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void SeedOnSceneAssignment(Entity entity, in PlayerCRDTEntity playerCRDTEntity)
        {
            if (!playerCRDTEntity.IsDirty) return;

            SetSceneTalking(in playerCRDTEntity, World.Has<IsActivelySpeakingTag>(entity));
        }

        private static void SetSceneTalking(in PlayerCRDTEntity playerCRDTEntity, bool talking)
        {
            if (playerCRDTEntity.SceneFacade is not { } sceneFacade) return;

            ref SDKTalking sdkTalking = ref sceneFacade.EcsExecutor.World.TryGetRef<SDKTalking>(playerCRDTEntity.SceneWorldEntity, out bool exists);

            if (exists)
                sdkTalking.Set(talking);
        }
    }
}
