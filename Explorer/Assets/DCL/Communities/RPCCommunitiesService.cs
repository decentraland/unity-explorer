using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using DCL.Web3.Identities;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
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
            socialServiceEventBus.RPCClientReconnected += OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished += OnTransportConnected;
        }

        public override void Dispose()
        {
            socialServiceEventBus.TransportClosed -= OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected -= OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished -= OnTransportConnected;
            subscriptionCts.SafeCancelAndDispose();
            base.Dispose();
        }

        private void OnTransportConnected()
        {
            if (identityCache.Identity == null) return;

            subscriptionCts = subscriptionCts.SafeRestart();
            TrySubscribeToConnectivityStatusAsync(subscriptionCts.Token).Forget();
        }

        private void OnTransportClosed()
        {
            subscriptionCts = subscriptionCts.SafeRestart();
        }

        private void OnTransportReconnected()
        {
            if (identityCache.Identity == null) return;

            subscriptionCts = subscriptionCts.SafeRestart();
            TrySubscribeToConnectivityStatusAsync(subscriptionCts.Token).Forget();
        }

        public async UniTask TrySubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            await KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<CommunityMemberConnectivityUpdate> stream =
                    socialServiceRPC.Module().CallServerStream<CommunityMemberConnectivityUpdate>(SUBSCRIBE_TO_CONNECTIVITY_UPDATES, new Empty());

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
