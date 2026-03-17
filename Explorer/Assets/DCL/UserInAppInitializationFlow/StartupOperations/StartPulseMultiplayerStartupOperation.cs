using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class StartPulseMultiplayerStartupOperation : StartUpOperationBase
    {
        private readonly PulseMultiplayerService service;
        private readonly IProfilePropagation profilePropagation;
        private readonly ISelfProfile selfProfile;

        public StartPulseMultiplayerStartupOperation(PulseMultiplayerService service,
            IProfilePropagation profilePropagation,
            ISelfProfile selfProfile)
        {
            this.service = service;
            this.profilePropagation = profilePropagation;
            this.selfProfile = selfProfile;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            await service.ConnectAsync(ct);
            Profile? profile = await selfProfile.ProfileAsync(ct);
            profilePropagation.Propagate(profile!);
        }
    }
}
