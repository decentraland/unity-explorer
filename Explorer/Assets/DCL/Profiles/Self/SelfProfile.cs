using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Fsm;

namespace DCL.Profiles.Self
{
    public class SelfProfile : ISelfProfile
    {
        private const string FSM_TAG = "SelfProfile";

        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly SelfProfileCmdExecutor executor;
        private readonly FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd> runtime;
        private readonly CancellationTokenSource drainCts = new ();
        private readonly CancellationToken drainToken;

        private long lastRequestId;

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
            drainToken = drainCts.Token;

            executor = new SelfProfileCmdExecutor(profileRepository, profileCache, wearableStorage, emoteStorage, equippedWearables, equippedEmotes,
                forcedWearables, forcedEmotes, world, playerEntity);

            runtime = new FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd>(FSM_TAG, SelfProfileModel.NoIdentity(), SelfProfileModel.Update, executor);

            web3IdentityCache.OnIdentityChanged += SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared += SendIdentityCleared;
            SendCurrentIdentity();

            DrainLoopAsync(drainCts.Token).Forget();
        }

        public void Dispose()
        {
            drainCts.SafeCancelAndDispose();
            web3IdentityCache.OnIdentityChanged -= SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared -= SendIdentityCleared;
            executor.Dispose();
        }

        public UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
        {
            RequestId id = NextRequestId();
            runtime.Send(SelfProfileMsg.FromProfileReadRequested(id));
            return AwaitResultAsync(id, static model => model.ReadResults, ProfileReadResult.FromError(ProfileReadError.Cancelled), ct);
        }

        public UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct)
        {
            RequestId id = NextRequestId();
            runtime.Send(SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(id, edited)));
            return AwaitResultAsync(id, static model => model.DeployResults, ProfileDeployResult.FromError(ProfileDeployError.Cancelled), ct);
        }

        /// <summary>Waits until the model holds the result of the request, then closes the request so the model drops it.</summary>
        private async UniTask<T> AwaitResultAsync<T>(RequestId id, Func<SelfProfileModel, RequestResults<T>> resultsOf, T cancelled, CancellationToken ct) where T: struct
        {
            try
            {
                while (!ct.IsCancellationRequested && !drainToken.IsCancellationRequested)
                {
                    if (resultsOf(CurrentProfileSnapshot).TryGet(id, out T result))
                        return result;

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                return cancelled;
            }
            finally { runtime.Send(SelfProfileMsg.FromRequestClosed(id)); }
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

        private RequestId NextRequestId() =>
            new (Interlocked.Increment(ref lastRequestId));

        /// <summary>Applies the queued messages once per frame, in the Initialization phase, until disposed.</summary>
        private async UniTaskVoid DrainLoopAsync(CancellationToken ct)
        {
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Initialization);

                if (ct.IsCancellationRequested)
                    return;

                runtime.Drain();
            }
        }
    }
}
