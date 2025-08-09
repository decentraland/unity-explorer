#nullable enable
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using Decentraland.SocialService.V2;
using DCL.WebRequests;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.Services
{
    public class RPCCommunityVoiceChatService : ICommunityVoiceService
    {
        /// <summary>
        ///     Timeout used for foreground operations
        /// </summary>
        private const int FOREGROUND_TIMEOUT_SECONDS = 10;

        private const string START_COMMUNITY_VOICE_CHAT = "StartCommunityVoiceChat";
        private const string JOIN_COMMUNITY_VOICE_CHAT = "JoinCommunityVoiceChat";
        private const string REQUEST_TO_SPEAK_COMMUNITY_VOICE_CHAT = "RequestToSpeakInCommunityVoiceChat";
        private const string PROMOTE_TO_SPEAKER_COMMUNITY_VOICE_CHAT = "PromoteSpeakerInCommunityVoiceChat";
        private const string REJECT_SPEAKER_COMMUNITY_VOICE_CHAT = "RejectSpeakRequestInCommunityVoiceChat";
        private const string DEMOTE_FROM_SPEAKER_COMMUNITY_VOICE_CHAT = "DemoteSpeakerInCommunityVoiceChat";
        private const string KICK_FROM_COMMUNITY_VOICE_CHAT = "KickPlayerFromCommunityVoiceChat";
        private const string END_COMMUNITY_VOICE_CHAT = "EndCommunityVoiceChat";
        private const string SUBSCRIBE_TO_COMMUNITY_VOICE_CHAT_UPDATES = "SubscribeToCommunityVoiceChatUpdates";

        private const string ACTIVE_COMMUNITY_VOICE_CHATS_ENDPOINT = "/v1/community-voice-chats/active";
        private const string SOCIAL_SERVICE_BASE_URL = "https://social-api.decentraland.zone";

        private readonly IRPCSocialServices socialServiceRPC;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IWebRequestController webRequestController;
        private CancellationTokenSource subscriptionCts = new();
        private bool isServiceDisabled = false;

        public event Action<CommunityVoiceChatUpdate>? CommunityVoiceChatUpdateReceived;
        public event Action<ActiveCommunityVoiceChatsResponse>? ActiveCommunityVoiceChatsFetched;

        public event Action? Reconnected;
        public event Action? Disconnected;

        public RPCCommunityVoiceChatService(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus,
            IWebRequestController webRequestController)
        {
            this.socialServiceRPC = socialServiceRPC;
            this.socialServiceEventBus = socialServiceEventBus;
            this.webRequestController = webRequestController;

            socialServiceEventBus.TransportClosed += OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected += OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished += OnTransportConnected;
        }

        private void OnTransportConnected()
        {
            if (!isServiceDisabled)
            {
                SubscribeToCommunityVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
                FetchActiveCommunityVoiceChatsAsync(subscriptionCts.Token).Forget();
            }
        }

        public void Dispose()
        {
            socialServiceEventBus.TransportClosed -= OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected -= OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished -= OnTransportConnected;
            subscriptionCts.SafeCancelAndDispose();
        }

        private void OnTransportClosed()
        {
            subscriptionCts = subscriptionCts.SafeRestart();
            Disconnected?.Invoke();
        }

        private void OnTransportReconnected()
        {
            if (!isServiceDisabled)
            {
                Reconnected?.Invoke();
                SubscribeToCommunityVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
                FetchActiveCommunityVoiceChatsAsync(subscriptionCts.Token).Forget();
            }
        }

        private void ThrowIfServiceDisabled()
        {
            if (isServiceDisabled)
            {
                //The caller should have proper error handling
                throw new InvalidOperationException("Voice chat service is disabled due to connection failures.");
            }
        }

        public async UniTask<StartCommunityVoiceChatResponse> StartCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new StartCommunityVoiceChatPayload
            {
                CommunityId = communityId
            };

            StartCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                              .CallUnaryProcedure<StartCommunityVoiceChatResponse>(START_COMMUNITY_VOICE_CHAT, payload)
                                                                              .AttachExternalCancellation(ct)
                                                                              .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<JoinCommunityVoiceChatResponse> JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new JoinCommunityVoiceChatPayload()
            {
                CommunityId = communityId
            };

            JoinCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                              .CallUnaryProcedure<JoinCommunityVoiceChatResponse>(JOIN_COMMUNITY_VOICE_CHAT, payload)
                                                                              .AttachExternalCancellation(ct)
                                                                              .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<EndCommunityVoiceChatResponse> EndCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new EndCommunityVoiceChatPayload()
            {
                CommunityId = communityId
            };

            EndCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                              .CallUnaryProcedure<EndCommunityVoiceChatResponse>(END_COMMUNITY_VOICE_CHAT, payload)
                                                                              .AttachExternalCancellation(ct)
                                                                              .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<ActiveCommunityVoiceChatsResponse> GetActiveCommunityVoiceChatsAsync(CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            var url = $"{SOCIAL_SERVICE_BASE_URL}{ACTIVE_COMMUNITY_VOICE_CHATS_ENDPOINT}";

            var result = await webRequestController
                .SignedFetchGetAsync(url, string.Empty, ct)
                .CreateFromJson<ActiveCommunityVoiceChatsResponse>(WRJsonParser.Newtonsoft);

            return result;
        }

        public async UniTask<RequestToSpeakInCommunityVoiceChatResponse> RequestToSpeakInCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new RequestToSpeakInCommunityVoiceChatPayload()
            {
                CommunityId = communityId,
            };

            RequestToSpeakInCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                             .CallUnaryProcedure<RequestToSpeakInCommunityVoiceChatResponse>(REQUEST_TO_SPEAK_COMMUNITY_VOICE_CHAT, payload)
                                                                             .AttachExternalCancellation(ct)
                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<PromoteSpeakerInCommunityVoiceChatResponse> PromoteSpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new PromoteSpeakerInCommunityVoiceChatPayload()
            {
                CommunityId = communityId,
                UserAddress = userAddress
            };

            PromoteSpeakerInCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                                         .CallUnaryProcedure<PromoteSpeakerInCommunityVoiceChatResponse>(PROMOTE_TO_SPEAKER_COMMUNITY_VOICE_CHAT, payload)
                                                                                         .AttachExternalCancellation(ct)
                                                                                         .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<RejectSpeakRequestInCommunityVoiceChatResponse> DenySpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new RejectSpeakRequestInCommunityVoiceChatPayload()
            {
                CommunityId = communityId,
                UserAddress = userAddress
            };

            RejectSpeakRequestInCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                                             .CallUnaryProcedure<RejectSpeakRequestInCommunityVoiceChatResponse>(REJECT_SPEAKER_COMMUNITY_VOICE_CHAT, payload)
                                                                                             .AttachExternalCancellation(ct)
                                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<DemoteSpeakerInCommunityVoiceChatResponse> DemoteSpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new DemoteSpeakerInCommunityVoiceChatPayload()
            {
                CommunityId = communityId,
                UserAddress = userAddress
            };

            DemoteSpeakerInCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                                        .CallUnaryProcedure<DemoteSpeakerInCommunityVoiceChatResponse>(DEMOTE_FROM_SPEAKER_COMMUNITY_VOICE_CHAT, payload)
                                                                                        .AttachExternalCancellation(ct)
                                                                                        .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<KickPlayerFromCommunityVoiceChatResponse> KickPlayerFromCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);
            var payload = new KickPlayerFromCommunityVoiceChatPayload()
            {
                CommunityId = communityId,
                UserAddress = userAddress
            };

            KickPlayerFromCommunityVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                                       .CallUnaryProcedure<KickPlayerFromCommunityVoiceChatResponse>(KICK_FROM_COMMUNITY_VOICE_CHAT, payload)
                                                                                       .AttachExternalCancellation(ct)
                                                                                       .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public UniTask SubscribeToCommunityVoiceChatUpdatesAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<CommunityVoiceChatUpdate> stream =
                    socialServiceRPC.Module()!.CallServerStream<CommunityVoiceChatUpdate>(SUBSCRIBE_TO_COMMUNITY_VOICE_CHAT_UPDATES, new Empty());

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Attempting to open community voice chat updates stream");

                try
                {
                    await foreach (CommunityVoiceChatUpdate? response in stream)
                    {
                        try
                        {
                            // Validate the response before processing
                            if (response == null)
                            {
                                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Received null community voice chat update");
                                continue;
                            }

                            CommunityVoiceChatUpdateReceived?.Invoke(response);
                        }
                        // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                        }
                    }
                }
                catch (InvalidOperationException e) when (e.Message.Contains("undefined"))
                {
                    ReportHub.LogError($"Server sent undefined data in community voice chat stream: {e.Message}", new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                    throw; // Re-throw to let KeepServerStreamOpenAsync handle the retry
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogError($"Unexpected error in community voice chat stream: {e.Message}", new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                    throw; // Re-throw to let KeepServerStreamOpenAsync handle the retry
                }
            }
        }

        private async UniTaskVoid FetchActiveCommunityVoiceChatsAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Fetching active community voice chats");
                var response = await GetActiveCommunityVoiceChatsAsync(ct);
                ActiveCommunityVoiceChatsFetched?.Invoke(response);
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Fetched {response.data.total} active community voice chats");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
            }
        }

        private async UniTask KeepServerStreamOpenAsync(Func<UniTask> openStreamFunc, CancellationToken ct)
        {
            // We try to keep the stream open until cancellation is requested
            // If for any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested && !isServiceDisabled)
            {
                try
                {
                    // It's an endless [background] loop
                    await socialServiceRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);
                    await openStreamFunc().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                }
            }
        }
    }
}
