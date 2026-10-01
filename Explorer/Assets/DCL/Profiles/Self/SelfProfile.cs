using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Threading;
using Utility.Fsm;

namespace DCL.Profiles.Self
{
    public class SelfProfile : ISelfProfile
    {
        private const string FSM_TAG = "SelfProfile";

        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly SelfProfileCmdExecutor executor;
        private readonly FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd> runtime;

        public SelfProfileModel CurrentProfileSnapshot => runtime.ModelSnapshot;

        public SelfProfile(
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IEquippedWearables equippedWearables,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IEquippedEmotes equippedEmotes,
            IReadOnlyList<URN>? forcedEmotes,
            IProfileCache profileCache,
            World world,
            Entity playerEntity,
            ForcedWearables forcedWearables)
        {
            this.web3IdentityCache = web3IdentityCache;

            executor = new SelfProfileCmdExecutor(profileRepository, profileCache, wearableStorage, emoteStorage, equippedWearables, equippedEmotes,
                forcedWearables, forcedEmotes, world, playerEntity);

            runtime = new FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd>(FSM_TAG, SelfProfileModel.NoIdentity(), SelfProfileModel.Update, executor);

            web3IdentityCache.OnIdentityChanged += SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared += SendIdentityCleared;
            SendCurrentIdentity();
        }

        public void Dispose()
        {
            web3IdentityCache.OnIdentityChanged -= SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared -= SendIdentityCleared;
            executor.Dispose();
        }

        /// <summary>Applies the queued messages. Call it once per frame on the main thread.</summary>
        public void Drain() => // TODO SelfProfile should manage the drain itself with UniTask and CancellationTokenSource, an update loop until dispoed. no external public drains
            runtime.Drain();

        // TODO too complex logic, both ProfileAsync and DeployProfileAsync. It would be better to store the lastProfile result alongside ID of the request, current approach manually splits the logic
        // and looses FSM
        public async UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
        {
            Option<ProfileFailure> retriedAfter = Option<ProfileFailure>.None;

            while (!ct.IsCancellationRequested)
            {
                if (!CurrentProfileSnapshot.IsIdentified(out Identified identified))
                    return ProfileReadResult.FromError(ProfileReadError.NoIdentity);

                if (identified.Knowledge.IsKnown(out Profile known))
                    return ProfileReadResult.FromOk(known);

                if (identified.Knowledge.IsMissing())
                    return ProfileReadResult.FromError(ProfileReadError.NotFound);

                if (identified.Knowledge.IsFailed(out ProfileFailure failure) && identified.Activity.IsIdle())
                {
                    // One retry per call: a retried read ends in a new failure instance.
                    if (!retriedAfter.Has)
                    {
                        retriedAfter = Option<ProfileFailure>.Some(failure);
                        runtime.Send(SelfProfileMsg.RetryRequested());
                    }
                    else if (!ReferenceEquals(retriedAfter.Value.Exception, failure.Exception))
                        return ProfileReadResult.FromError(ProfileReadError.ReadFailed);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return ProfileReadResult.FromError(ProfileReadError.Cancelled);
        }

        public async UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return ProfileDeployResult.FromError(ProfileDeployError.Cancelled);

            if (!CurrentProfileSnapshot.IsIdentified(out Identified identified))
                return ProfileDeployResult.FromError(ProfileDeployError.NoIdentity);

            int baseVersion = edited.Version;

            if (identified.Knowledge.IsKnown(out Profile known))
            {
                if (edited.IsSameProfile(known))
                    return ProfileDeployResult.FromError(ProfileDeployError.NothingChanged);

                baseVersion = known.Version;
            }

            runtime.Send(SelfProfileMsg.FromProfileEdited(edited));
            return await DeployOutcomeAsync(identified.Address, edited, baseVersion, ct);
        }

        private async UniTask<ProfileDeployResult> DeployOutcomeAsync(UserId address, Profile edited, int baseVersion, CancellationToken ct)
        {
            Profile awaited = edited;

            while (!ct.IsCancellationRequested)
            {
                if (!CurrentProfileSnapshot.IsIdentified(out Identified identified) || !identified.Address.Equals(address))
                    return ProfileDeployResult.FromError(ProfileDeployError.NoIdentity);

                if (identified.LastDeployFailure.Has && ReferenceEquals(identified.LastDeployFailure.Value.Sent, awaited))
                    return ProfileDeployResult.FromError(ProfileDeployError.DeployFailed);

                // A later edit supersedes the awaited one and carries its changes, so its outcome becomes this call's outcome.
                if (identified.Activity.IsDeploying(out Deploying deploying))
                    awaited = deploying.Pending;
                else if (identified.Knowledge.IsKnown(out Profile saved) && saved.Version > baseVersion)
                    return ProfileDeployResult.FromOk(saved);

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return ProfileDeployResult.FromError(ProfileDeployError.Cancelled);
        }

        private void SendCurrentIdentity()
        {
            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null)
            {
                runtime.Send(SelfProfileMsg.IdentityCleared());
                return;
            }

            Option<UserId> address = UserId.From(identity.Address);

            // An identity without an address cannot own a profile; it counts as no identity.
            runtime.Send(address.Has ? SelfProfileMsg.FromIdentityChanged(address.Value) : SelfProfileMsg.IdentityCleared());
        }

        private void SendIdentityCleared() =>
            runtime.Send(SelfProfileMsg.IdentityCleared());
    }
}
