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
    /// <summary>Thread-safe</summary>
    public class SelfProfile : IDisposable
    {
        private const string FSM_TAG = "SelfProfile";

        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd> runtime;
        private readonly CancellationTokenSource drainCts = new ();
        private readonly CancellationToken drainToken;

        /// <summary>Taking an id and enqueueing its request happen under this gate, so requests enter the inbox in id order.</summary>
        private readonly object requestGate = new ();

        private long lastRequestId;

        public virtual SelfProfileModel CurrentProfileSnapshot => runtime.ModelSnapshot;

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

            var executor = new SelfProfileCmdExecutor(profileRepository, profileCache, wearableStorage, emoteStorage, equippedWearables, equippedEmotes,
                forcedWearables, forcedEmotes, world, playerEntity);

            runtime = new FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd>(FSM_TAG, SelfProfileModel.NoIdentity(), SelfProfileModel.Update, executor);

            web3IdentityCache.OnIdentityChanged += SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared += SendIdentityCleared;
            SendCurrentIdentity();

            DrainLoopAsync(drainCts.Token).Forget();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        ///     Test seam: no identity events and no drain loop, the model stays at <c>NoIdentity</c>.
        ///     Substitutes and fakes override the virtual members.
        /// </summary>
        protected SelfProfile()
        {
            web3IdentityCache = new MemoryWeb3IdentityCache();
            drainToken = drainCts.Token;
            runtime = new FsmRuntime<SelfProfileModel, SelfProfileMsg, SelfProfileCmd>(FSM_TAG, SelfProfileModel.NoIdentity(), SelfProfileModel.Update, new InertCmdExecutor());
        }
#endif

        public virtual void Dispose()
        {
            drainCts.SafeCancelAndDispose();
            web3IdentityCache.OnIdentityChanged -= SendCurrentIdentity;
            web3IdentityCache.OnIdentityCleared -= SendIdentityCleared;
            runtime.Dispose();
        }

        /// <summary>
        ///     Waits until the current identity's profile is resolved. A failed read is retried once per call.
        /// </summary>
        public virtual UniTask<ProfileReadResult> ProfileAsync(CancellationToken ct)
        {
            IMsgInbox<SelfProfileMsg> inbox = runtime;
            RequestId id;

            lock (requestGate) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            {
                id = NextRequestId();
                inbox.Send(SelfProfileMsg.FromProfileReadRequested(id));
            }

            return AwaitResultAsync(id, static model => model.ReadResults, ProfileReadResult.FromError(ProfileReadError.Cancelled), ct);
        }

        /// <summary>
        ///     Cancelling the token stops waiting; the deploy itself always runs to its end.
        /// </summary>
        public virtual UniTask<ProfileDeployResult> DeployProfileAsync(Profile edited, CancellationToken ct)
        {
            IMsgInbox<SelfProfileMsg> inbox = runtime;
            RequestId id;

            lock (requestGate) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            {
                id = NextRequestId();
                inbox.Send(SelfProfileMsg.FromDeployProfileOnEditRequested(new DeployRequest(id, edited)));
            }

            return AwaitResultAsync(id, static model => model.DeployResults, ProfileDeployResult.FromError(ProfileDeployError.Cancelled), ct);
        }

        /// <summary>
        ///     Waits until the model holds the result of the request, then closes the request so the model drops it.
        ///     A request the model has applied but no longer holds was dropped from a full list; it resolves as cancelled.
        /// </summary>
        private async UniTask<T> AwaitResultAsync<T>(RequestId id, Func<SelfProfileModel, RequestResults<T>> resultsOf, T cancelled, CancellationToken ct) where T: struct
        {
            try
            {
                while (!ct.IsCancellationRequested && !drainToken.IsCancellationRequested)
                {
                    SelfProfileModel model = CurrentProfileSnapshot;

                    if (resultsOf(model).TryGet(id, out T result))
                        return result;

                    if (model.LastRequest.Value >= id.Value && !model.Holds(id))
                        return cancelled;

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
            new (++lastRequestId);

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

#if UNITY_INCLUDE_TESTS
        private class InertCmdExecutor : ICmdExecutor<SelfProfileCmd, SelfProfileMsg>
        {
            public void Dispose() { }

            public void Execute(in SelfProfileCmd cmd, IMsgInbox<SelfProfileMsg> inbox) { }
        }
#endif
    }
}
