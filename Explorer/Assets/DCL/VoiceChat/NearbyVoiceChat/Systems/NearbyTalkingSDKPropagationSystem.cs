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
    ///     A remote player's <see cref="IsActivelySpeakingTag" /> state is written every frame to the <see cref="SDKTalking" />
    ///     of the scene the player stands in; <see cref="SDKTalking.Set" /> raises it only when the value changes.
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
            // local player
            World.Get<TalkingComponent>(playerEntity).IsTalking = stateModel.IsLocalSpeaking;

            // remote players
            PropagateRemoteTalkingQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void PropagateRemoteTalking(Entity entity, in PlayerCRDTEntity playerCRDTEntity)
        {
            if (!playerCRDTEntity.AssignedToScene) return;

            ref SDKTalking sdkTalking = ref playerCRDTEntity.SceneFacade!.EcsExecutor.World.TryGetRef<SDKTalking>(playerCRDTEntity.SceneWorldEntity, out bool exists);

            if (exists)
                sdkTalking.Set(World.Has<IsActivelySpeakingTag>(entity));
        }
    }
}
