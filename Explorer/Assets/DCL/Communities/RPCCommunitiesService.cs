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
    public class RPCCommunitiesService : IRPCCommunitiesService
    {
        private const string SUBSCRIBE_TO_CONNECTIVITY_UPDATES = "SubscribeToCommunityMemberConnectivityUpdates";

        private readonly IRPCSocialServices socialServiceRPC;
        private readonly CommunitiesEventBus communitiesEventBus;

        public RPCCommunitiesService(
            IRPCSocialServices socialServiceRPC,
            CommunitiesEventBus communitiesEventBus)
        {
            this.socialServiceRPC = socialServiceRPC;
            this.communitiesEventBus = communitiesEventBus;
        }

        public void Dispose()
        {
        }

        private async UniTask KeepServerStreamOpenAsync(Func<UniTask> openStreamFunc, CancellationToken ct)
        {
            // We try to keep the stream open until cancellation is requested
            // If for any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // It's an endless [background] loop
                    await socialServiceRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);
                    await openStreamFunc().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES)); }
            }
        }

        public UniTask SubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<CommunityMemberConnectivityUpdate> stream =
                    socialServiceRPC.Module()!.CallServerStream<CommunityMemberConnectivityUpdate>(SUBSCRIBE_TO_CONNECTIVITY_UPDATES, new Empty());

                // We could try stream.WithCancellation(ct) but the cancellation doesn't work.
                await foreach (CommunityMemberConnectivityUpdate? response in stream)
                {
                    //Debug.Log($"RPC: {response.Member.Address}, {response.CommunityId}, {response.Status}");

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
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES)); }
                }
            }
        }
    }
}
