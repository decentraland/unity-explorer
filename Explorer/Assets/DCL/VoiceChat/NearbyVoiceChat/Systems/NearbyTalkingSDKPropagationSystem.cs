using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Resolves each player's Nearby voice activity, exposed to scenes as <c>PBAvatarBase.talking</c>.
    ///     A remote player's value goes to the <see cref="SDKTalking" /> of the scene the player stands in;
    ///     <see cref="SDKTalking.Set" /> marks it for a rewrite only when the value flips.
    ///     Remote activity is read from <see cref="IsActivelySpeakingTag" /> as is: whether the local player hears
    ///     the speaker (mute, block, scene ban, listening state) does not change the fact that the avatar is talking.
    ///     The local player's value goes to <see cref="TalkingComponent" />, which
    ///     <see cref="DCL.Multiplayer.SDK.Systems.SceneWorld.LocalPlayerCRDTEntityHandlerSystem" /> mirrors into every scene.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerProfileDataPropagationSystem))]
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
            MarkRemotePlayerTalkingQuery(World);
            MarkRemotePlayerSilentQuery(World);
        }

        [Query]
        [All(typeof(IsActivelySpeakingTag))]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void MarkRemotePlayerTalking(in PlayerCRDTEntity playerCRDTEntity)
        {
            SetSceneTalking(in playerCRDTEntity, true);
        }

        [Query]
        [None(typeof(IsActivelySpeakingTag), typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void MarkRemotePlayerSilent(in PlayerCRDTEntity playerCRDTEntity)
        {
            SetSceneTalking(in playerCRDTEntity, false);
        }

        private static void SetSceneTalking(in PlayerCRDTEntity playerCRDTEntity, bool talking)
        {
            if (!playerCRDTEntity.AssignedToScene) return;

            ref SDKTalking sdkTalking = ref playerCRDTEntity.SceneFacade!.EcsExecutor.World.TryGetRef<SDKTalking>(playerCRDTEntity.SceneWorldEntity, out bool exists);

            if (exists)
                sdkTalking.Set(talking);
        }
    }
}
