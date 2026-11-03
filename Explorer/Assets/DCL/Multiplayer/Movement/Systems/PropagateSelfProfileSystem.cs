using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utility.Types;
using ECS.Abstract;

namespace DCL.Multiplayer.Movement
{
    /// <summary>Announces the self profile the catalyst confirmed to Pulse while Pulse is active; an edit still deploying is not announced.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER)]
    public partial class PropagateSelfProfileSystem : BaseUnityLoopSystem
    {
        private readonly SelfProfile selfProfile;
        private readonly IProfilePropagation profilePropagation;
        private readonly PulseActivation pulseActivation;

        internal PropagateSelfProfileSystem(World world, SelfProfile selfProfile, IProfilePropagation profilePropagation, PulseActivation pulseActivation) : base(world)
        {
            this.selfProfile = selfProfile;
            this.profilePropagation = profilePropagation;
            this.pulseActivation = pulseActivation;
        }

        protected override void Update(float t)
        {
            if (!pulseActivation.IsActive)
                return;

            Option<Profile> confirmed = selfProfile.CurrentProfileSnapshot.ConfirmedProfile;

            if (confirmed.Has)
                profilePropagation.PropagateIfNewVersion(confirmed.Value);
        }
    }
}
