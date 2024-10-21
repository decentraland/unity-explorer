using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Character;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.UserInAppInitializationFlow;
using ECS;
using ECS.Abstract;

namespace DCL.Multiplayer.Profiles.Systems
{
    /// <summary>
    ///     The scheme
    ///     1 receive signal announce profile
    ///     2 fetch the profile
    ///     3 assign profile to the entity
    ///     4 auto flow of avatar
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AvatarGroup))]
    public partial class MultiplayerProfilesSystem : BaseUnityLoopSystem
    {
        private readonly IRemoteAnnouncements remoteAnnouncements;
        private readonly IRemoveIntentions removeIntentions;
        private readonly IRemoteProfiles remoteProfiles;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IRemoteEntities remoteEntities;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly ICharacterObject characterObject;
        private readonly ILoadingStatus realFlowLoadingStatus;
        private readonly IRealmData realmData;

        public MultiplayerProfilesSystem(
            World world,
            IRemoteAnnouncements remoteAnnouncements,
            IRemoveIntentions removeIntentions,
            IRemoteProfiles remoteProfiles,
            IProfileBroadcast profileBroadcast,
            IRemoteEntities remoteEntities,
            IRemoteMetadata remoteMetadata,
            ICharacterObject characterObject,
            ILoadingStatus realFlowLoadingStatus,
            IRealmData realmData
        ) : base(world)
        {
            this.remoteAnnouncements = remoteAnnouncements;
            this.removeIntentions = removeIntentions;
            this.remoteProfiles = remoteProfiles;
            this.profileBroadcast = profileBroadcast;
            this.remoteEntities = remoteEntities;
            this.remoteMetadata = remoteMetadata;
            this.characterObject = characterObject;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            if (realFlowLoadingStatus.CurrentStage.Value is not LoadingStatus.LoadingStage.Completed)
                return;

            // On realm switch it may be not configured yet
            if (!realmData.Configured)
                return;

            remoteMetadata.BroadcastSelfMetadata();
            remoteMetadata.BroadcastSelfParcel(characterObject);
            remoteProfiles.Download(remoteAnnouncements);
            remoteEntities.TryCreate(remoteProfiles, World!);
            remoteEntities.Remove(removeIntentions, World!);
            profileBroadcast.NotifyRemotes();
        }
    }
}
