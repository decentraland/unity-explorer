using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading;

namespace DCL.Communities
{
    public class RPCCommunitiesService : RPCSocialServiceBase
    {
        private const string SUBSCRIBE_TO_CONNECTIVITY_UPDATES = "SubscribeToCommunityMemberConnectivityUpdates";
        // Increase the default number of retries because once it consumes all, it will not receive updates for the rest of the session
        private const int MAX_CONNECTION_RETRIES = 20;

        private readonly CommunitiesEventBus communitiesEventBus;
        private bool isListeningToUpdatesFromServer;

        public RPCCommunitiesService(
            IRPCSocialServices socialServiceRPC,
            CommunitiesEventBus communitiesEventBus) : base(socialServiceRPC, ReportCategory.COMMUNITIES, MAX_CONNECTION_RETRIES)
        {
            this.communitiesEventBus = communitiesEventBus;
        }

        public async UniTask TrySubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            if (isListeningToUpdatesFromServer) return;

            try
            {
                isListeningToUpdatesFromServer = true;
                await KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);
            }
            finally { isListeningToUpdatesFromServer = false; }

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<CommunityMemberConnectivityUpdate> stream =
                    socialServiceRPC.Module().CallServerStream<CommunityMemberConnectivityUpdate>(SUBSCRIBE_TO_CONNECTIVITY_UPDATES, new Empty());

                // We could try stream.WithCancellation(ct) but the cancellation doesn't work.
                await foreach (CommunityMemberConnectivityUpdate? response in stream)
                {
                    try
                    {
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

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    // No need to handle OperationCancelledException because there are no async calls
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.COMMUNITIES); }
                }
            }
        }
    }
}
