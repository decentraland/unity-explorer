using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.Character;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.UserInAppInitializationFlow;
using ECS.Abstract;
using UnityEngine;
using Utility;

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
    [UpdateBefore(typeof(AvatarInstantiatorSystem))]
    public partial class MultiplayerProfilesSystem : BaseUnityLoopSystem
    {
        private readonly IRemoteAnnouncements remoteAnnouncements;
        private readonly IRemoveIntentions removeIntentions;
        private readonly IRemoteProfiles remoteProfiles;
        private readonly IProfileBroadcast profileBroadcast;
        private readonly IRemoteEntities remoteEntities;
        private readonly IRemotePoses remotePoses;
        private readonly ICharacterObject characterObject;
        private readonly IReadOnlyRealFlowLoadingStatus realFlowLoadingStatus;

        public MultiplayerProfilesSystem(
            World world,
            IRemoteAnnouncements remoteAnnouncements,
            IRemoveIntentions removeIntentions,
            IRemoteProfiles remoteProfiles,
            IProfileBroadcast profileBroadcast,
            IRemoteEntities remoteEntities,
            IRemotePoses remotePoses,
            ICharacterObject characterObject,
            IReadOnlyRealFlowLoadingStatus realFlowLoadingStatus
        ) : base(world)
        {
            this.remoteAnnouncements = remoteAnnouncements;
            this.removeIntentions = removeIntentions;
            this.remoteProfiles = remoteProfiles;
            this.profileBroadcast = profileBroadcast;
            this.remoteEntities = remoteEntities;
            this.remotePoses = remotePoses;
            this.characterObject = characterObject;
            this.realFlowLoadingStatus = realFlowLoadingStatus;
        }

        protected override void Update(float t)
        {
            if (realFlowLoadingStatus.CurrentStage is not RealFlowLoadingStatus.Stage.Completed)
                return;

            remoteProfiles.Download(remoteAnnouncements);
            remoteEntities.TryCreate(remoteProfiles, World!);
            remoteEntities.Remove(removeIntentions, World!);
            profileBroadcast.NotifyRemotes();
            remotePoses.BroadcastSelfPose(characterObject);
        }
    }
}
