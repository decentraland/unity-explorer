using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utility.Types;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class StartPulseMultiplayerStartupOperation : StartUpOperationBase
    {
        private const int CONNECTION_ATTEMPTS = 5;

        private readonly IPulseMultiplayerService service;
        private readonly IProfilePropagation profilePropagation;
        private readonly SelfProfile selfProfile;
        private readonly PulseActivation pulseActivation;
        private readonly PulseRealm pulseRealm;

        public StartPulseMultiplayerStartupOperation(IPulseMultiplayerService service,
            IProfilePropagation profilePropagation,
            SelfProfile selfProfile,
            PulseActivation pulseActivation,
            PulseRealm pulseRealm)
        {
            this.service = service;
            this.profilePropagation = profilePropagation;
            this.selfProfile = selfProfile;
            this.pulseActivation = pulseActivation;
            this.pulseRealm = pulseRealm;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            // Pulse disabled (feature off / --pulse false) or already fell back in a previous flow — nothing to start.
            if (!pulseActivation.IsActive)
                return;

            // Resolved before connecting: the realm goes out in the very first message (the handshake's
            // initial state), and an empty one violates the server contract.
            await pulseRealm.EnsureResolvedAsync(ct);

            if (string.IsNullOrEmpty(pulseRealm.Value))
            {
                // In local scene development this means the dev server is unreachable; fall back to LiveKit-only, as before this transport existed.
                pulseActivation.Deactivate();
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, "Pulse realm could not be resolved at start-up; falling back to LiveKit-only.");
                await UniTask.SwitchToMainThread();
                return;
            }

            if (!await service.ConnectAsync(ct, CONNECTION_ATTEMPTS))
            {
                // Server is unreachable or keeps failing the handshake: fall back fully to LiveKit so the client behaves as if Pulse were absent.
                pulseActivation.Deactivate();
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, "Pulse connection failed at start-up; falling back to LiveKit-only.");
                await UniTask.SwitchToMainThread();
                return;
            }

            ProfileReadResult read = await selfProfile.ProfileAsync(ct);

            if (read.IsCancelled)
                throw new OperationCanceledException(ct);

            if (!read.IsOk(out _))
                throw new InvalidOperationException($"Own profile could not be resolved ({read}), nothing to propagate to Pulse");

            // Only the version the catalyst confirmed is propagated, never a pending edit's.
            Option<Profile> confirmed = selfProfile.CurrentProfileSnapshot.ConfirmedProfile;

            if (!confirmed.Has)
                return;

            await UniTask.SwitchToMainThread();
            profilePropagation.PropagateIfNewVersion(confirmed.Value);
        }
    }
}
