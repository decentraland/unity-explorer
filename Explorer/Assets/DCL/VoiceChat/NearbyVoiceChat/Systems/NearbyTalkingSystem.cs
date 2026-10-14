using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Resolves each player's Nearby voice activity, exposed to scenes as <c>PBAvatarBase.talking</c>.
    ///     A remote player's value goes to the <see cref="SDKProfile" /> of the scene the player stands in;
    ///     <see cref="SDKProfile.SetTalking" /> marks it for a rewrite only when the value flips.
    ///     The local player's value goes to <see cref="LocalPlayerTalkingComponent" />, which
    ///     <see cref="DCL.Multiplayer.SDK.Systems.SceneWorld.LocalPlayerCRDTEntityHandlerSystem" /> mirrors into every scene.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerProfileDataPropagationSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyTalkingSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly INearbyAudioStreamRegistry registry;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyMuteService muteService;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly RoomMetadataCurrentScene roomMetadataCurrentScene;

        internal NearbyTalkingSystem(
            World world,
            Entity playerEntity,
            INearbyAudioStreamRegistry registry,
            NearbyVoiceChatStateModel stateModel,
            NearbyMuteService muteService,
            IUserBlockingCache userBlockingCache,
            RoomMetadataCurrentScene roomMetadataCurrentScene) : base(world)
        {
            this.playerEntity = playerEntity;
            this.registry = registry;
            this.stateModel = stateModel;
            this.muteService = muteService;
            this.userBlockingCache = userBlockingCache;
            this.roomMetadataCurrentScene = roomMetadataCurrentScene;
        }

        protected override void Update(float t)
        {
            UpdateLocalPlayerTalking();
            UpdateRemotePlayerTalkingQuery(World);
        }

        private void UpdateLocalPlayerTalking()
        {
            if (!World.TryGet(playerEntity, out Profile? profile))
                return;

            World.AddOrGet<LocalPlayerTalkingComponent>(playerEntity).IsTalking =
                stateModel.State.Value == NearbyVoiceChatState.OpenMic && registry.IsActiveSpeaker(profile!.UserId.Value);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void UpdateRemotePlayerTalking(Profile profile, in PlayerCRDTEntity playerCRDTEntity)
        {
            if (playerCRDTEntity.SceneFacade is not { } scene
                || !scene.EcsExecutor.World.TryGet(playerCRDTEntity.SceneWorldEntity, out SDKProfile? sdkProfile))
                return;

            string walletId = profile.UserId.Value;

            bool talking = registry.IsActiveSpeaker(walletId)
                           && !stateModel.IsListeningDisabled
                           && !userBlockingCache.UserIsBlocked(walletId)
                           && !roomMetadataCurrentScene.IsUserBanned(walletId)
                           && !muteService.IsMuted(walletId);

            sdkProfile!.SetTalking(talking);
        }
    }
}
