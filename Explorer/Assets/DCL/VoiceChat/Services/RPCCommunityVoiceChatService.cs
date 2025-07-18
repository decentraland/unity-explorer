using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using Decentraland.SocialService.V2;
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

        /// <summary>
        ///     Maximum number of retry attempts for server stream connection
        /// </summary>
        private const int MAX_STREAM_RETRY_ATTEMPTS = 5;

        /// <summary>
        ///     Base delay in seconds between retry attempts (will be exponentially increased)
        /// </summary>
        private const int BASE_RETRY_DELAY_SECONDS = 2;

        private const string START_COMMUNITY_VOICE_CHAT = "StartCommunityVoiceChat";
        private const string JOIN_COMMUNITY_VOICE_CHAT = "JoinCommunityVoiceChat";
        private const string REQUEST_TO_SPEAK_COMMUNITY_VOICE_CHAT = "RequestToSpeakInCommunityVoiceChat";
        private const string PROMOTE_TO_SPEAKER_COMMUNITY_VOICE_CHAT = "PromoteSpeakerInCommunityVoiceChat";
        private const string DEMOTE_FROM_SPEAKER_COMMUNITY_VOICE_CHAT = "DemoteSpeakerInCommunityVoiceChat";
        private const string KICK_FROM_COMMUNITY_VOICE_CHAT = "KickPlayerFromCommunityVoiceChat";
        private const string SUBSCRIBE_TO_COMMUNITY_VOICE_CHAT_UPDATES = "SubscribeToCommunityVoiceChatUpdates";

        private readonly IRPCSocialServices socialServiceRPC;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private CancellationTokenSource subscriptionCts = new();
        private bool isServiceDisabled = false;

        public event Action<CommunityVoiceChatUpdate> CommunityVoiceChatUpdateReceived;

        public event Action Reconnected;
        public event Action Disconnected;

        public RPCCommunityVoiceChatService(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus)
        {
            this.socialServiceRPC = socialServiceRPC;
            this.socialServiceEventBus = socialServiceEventBus;

            socialServiceEventBus.TransportClosed += OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected += OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished += OnTransportConnected;
        }

        private void OnTransportConnected()
        {
            if (!isServiceDisabled)
            {
                SubscribeToCommunityVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
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
                int retryAttempt = 0;
                bool streamOpened = false;

                while (retryAttempt < MAX_STREAM_RETRY_ATTEMPTS && !ct.IsCancellationRequested)
                {
                    try
                    {
                        IUniTaskAsyncEnumerable<CommunityVoiceChatUpdate> stream =
                            socialServiceRPC.Module()!.CallServerStream<CommunityVoiceChatUpdate>(SUBSCRIBE_TO_COMMUNITY_VOICE_CHAT_UPDATES, new Empty());

                        streamOpened = true;
                        ReportHub.Log(ReportCategory.VOICE_CHAT, "Successfully opened community voice chat updates stream");

                        await foreach (CommunityVoiceChatUpdate? response in stream)
                        {
                            try
                            {
                                CommunityVoiceChatUpdateReceived?.Invoke(response);
                            }
                            // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT)); }
                        }

                        // If we reach here, the stream has ended normally
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested, exit the retry loop
                        break;
                    }
                    catch (Exception e)
                    {
                        retryAttempt++;
                        ReportHub.LogError($"Failed to open community voice chat updates stream (attempt {retryAttempt}/{MAX_STREAM_RETRY_ATTEMPTS} exception {e}", new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));

                        if (retryAttempt >= MAX_STREAM_RETRY_ATTEMPTS)
                        {
                            ReportHub.LogError($"Failed to open community voice chat updates stream after {MAX_STREAM_RETRY_ATTEMPTS} attempts. Disabling voice chat service.", new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                            isServiceDisabled = true;
                            break;
                        }

                        // Calculate exponential backoff delay
                        int delaySeconds = BASE_RETRY_DELAY_SECONDS * (int)Math.Pow(2, retryAttempt - 1);
                        ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Retrying community voice chat updates stream connection in {delaySeconds} seconds...");

                        try
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancellation requested during delay, exit the retry loop
                            break;
                        }
                    }
                }

                if (!streamOpened && !ct.IsCancellationRequested)
                {
                    ReportHub.LogError("Failed to establish community voice chat updates stream after all retry attempts", new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
                }
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
