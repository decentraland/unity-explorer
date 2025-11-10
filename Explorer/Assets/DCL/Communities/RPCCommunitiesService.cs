using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    public class RPCCommunitiesService : RPCSocialServiceBase, IRPCCommunitiesService
    {
        private const string SUBSCRIBE_TO_CONNECTIVITY_UPDATES = "SubscribeToCommunityMemberConnectivityUpdates";

        private readonly CommunitiesEventBus communitiesEventBus;

        public RPCCommunitiesService(
            IRPCSocialServices socialServiceRPC,
            CommunitiesEventBus communitiesEventBus) : base(socialServiceRPC, ReportCategory.COMMUNITIES)
        {
            this.communitiesEventBus = communitiesEventBus;
        }

        public UniTask SubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

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
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        DiagnosticInfoUtils.LogWebSocketException(e, ReportCategory.COMMUNITIES);
                    }
                }
            }
        }
    }
}
