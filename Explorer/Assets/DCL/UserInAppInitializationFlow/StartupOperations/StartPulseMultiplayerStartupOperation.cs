using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class StartPulseMultiplayerStartupOperation : StartUpOperationBase
    {
        private const int CONNECTION_ATTEMPTS = 5;

        private readonly IPulseMultiplayerService service;
        private readonly IProfilePropagation profilePropagation;
        private readonly ISelfProfile selfProfile;
        private readonly PulseActivation pulseActivation;

        public StartPulseMultiplayerStartupOperation(IPulseMultiplayerService service,
            IProfilePropagation profilePropagation,
            ISelfProfile selfProfile,
            PulseActivation pulseActivation)
        {
            this.service = service;
            this.profilePropagation = profilePropagation;
            this.selfProfile = selfProfile;
            this.pulseActivation = pulseActivation;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            // Pulse disabled (feature off / --pulse false) or already fell back in a previous flow — nothing to start.
            if (!pulseActivation.IsActive)
                return;

            if (!await service.ConnectAsync(ct, CONNECTION_ATTEMPTS))
            {
                // Server is unreachable: fall back fully to LiveKit so the client behaves as if Pulse were absent.
                pulseActivation.Deactivate();
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, "Pulse unreachable at start-up; falling back to LiveKit-only.");
                await UniTask.SwitchToMainThread();
                return;
            }

            Profile? profile = await selfProfile.ProfileAsync(ct);
            profilePropagation.Propagate(profile!);
            await UniTask.SwitchToMainThread();
        }
    }
}
