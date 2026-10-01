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
    /// <summary>Announces the trusted self profile to Pulse while Pulse is active.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER)]
    public partial class PropagateSelfProfileSystem : BaseUnityLoopSystem
    {
        private readonly ISelfProfile selfProfile;
        private readonly IProfilePropagation profilePropagation;
        private readonly PulseActivation pulseActivation;

        internal PropagateSelfProfileSystem(World world, ISelfProfile selfProfile, IProfilePropagation profilePropagation, PulseActivation pulseActivation) : base(world)
        {
            this.selfProfile = selfProfile;
            this.profilePropagation = profilePropagation;
            this.pulseActivation = pulseActivation;
        }

        protected override void Update(float t)
        {
            if (!pulseActivation.IsActive)
                return;

            Option<Profile> known = selfProfile.CurrentProfileSnapshot.KnownProfile;

            if (known.Has)
                profilePropagation.PropagateIfNewVersion(known.Value);
        }
    }
}
