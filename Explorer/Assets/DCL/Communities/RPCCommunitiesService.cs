using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using DCL.Web3.Identities;
using Decentraland.SocialService.V2;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities
{
    public class RPCCommunitiesService : RPCSocialServiceBase
    {
        private const string SUBSCRIBE_TO_CONNECTIVITY_UPDATES = "SubscribeToCommunityMemberConnectivityUpdates";
        // Increase the default number of retries because once it consumes all, it will not receive updates for the rest of the session
        private const int MAX_CONNECTION_RETRIES = 20;

        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IWeb3IdentityCache identityCache;
        private CancellationTokenSource subscriptionCts = new ();

        public RPCCommunitiesService(
            IRPCSocialServices socialServiceRPC,
            CommunitiesEventBus communitiesEventBus,
            ISocialServiceEventBus socialServiceEventBus,
            IWeb3IdentityCache identityCache) : base(socialServiceRPC, ReportCategory.COMMUNITIES, MAX_CONNECTION_RETRIES)
        {
            this.communitiesEventBus = communitiesEventBus;
            this.socialServiceEventBus = socialServiceEventBus;
            this.identityCache = identityCache;

            socialServiceEventBus.TransportClosed += OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected += SubscribeToConnectivityStatus;
            socialServiceEventBus.WebSocketConnectionEstablished += SubscribeToConnectivityStatus;
        }

        public override void Dispose()
        {
            socialServiceEventBus.TransportClosed -= OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected -= SubscribeToConnectivityStatus;
            socialServiceEventBus.WebSocketConnectionEstablished -= SubscribeToConnectivityStatus;
            subscriptionCts.SafeCancelAndDispose();
            base.Dispose();
        }

        /// <summary>
        ///     Starts the connectivity updates subscription. A call while the subscription is already
        ///     active is a no-op, enforced by <see cref="RPCSocialServiceBase.KeepServerStreamOpenAsync{T}" />.
        /// </summary>
        public void SubscribeToConnectivityStatus()
        {
            if (identityCache.Identity == null) return;

            TrySubscribeToConnectivityStatusAsync(subscriptionCts.Token).Forget();
        }

        private void OnTransportClosed()
        {
            subscriptionCts = subscriptionCts.SafeRestart();
        }

        private async UniTask TrySubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            await KeepServerStreamOpenAsync<CommunityMemberConnectivityUpdate>(OpenStreamAndProcessUpdatesAsync, SUBSCRIBE_TO_CONNECTIVITY_UPDATES, ct);

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync(IUniTaskAsyncEnumerable<CommunityMemberConnectivityUpdate> stream)
            {
                await foreach (CommunityMemberConnectivityUpdate? response in EnumerateWithCancellationAsync(stream, ct))
                {
                    try
                    {
                        //If we are disconnecting from the social service rpc, avoid processing events
                        //that would cause exception later down the flow
                        if (socialServiceRPC.IsDisconnecting) continue;

                        switch (response.Status)
                        {
                            case ConnectivityStatus.Offline:
                                communitiesEventBus.BroadcastUserDisconnectedFromCommunity(response);
                                break;
                            case ConnectivityStatus.Online:
                                communitiesEventBus.BroadcastUserConnectedToCommunity(response);
                                break;
                        }
                    }

                    catch (OperationCanceledException) { }
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }
                }
            }
        }
    }
}
