using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.SocialService;
using System;
using System.Threading;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
using Utility;

namespace DCL.VoiceChat.Services
{
    public class RPCPrivateVoiceChatService : RPCSocialServiceBase, IVoiceService
    {
        private const string TAG = nameof(RPCPrivateVoiceChatService);

        public event Action<PrivateVoiceChatUpdate>? PrivateVoiceChatUpdateReceived;
        public event Action? Reconnected;
        public event Action? Disconnected;

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

        private const string START_PRIVATE_VOICE_CHAT = "StartPrivateVoiceChat";
        private const string ACCEPT_PRIVATE_VOICE_CHAT = "AcceptPrivateVoiceChat";
        private const string REJECT_PRIVATE_VOICE_CHAT = "RejectPrivateVoiceChat";
        private const string END_PRIVATE_VOICE_CHAT = "EndPrivateVoiceChat";
        private const string SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES = "SubscribeToPrivateVoiceChatUpdates";
        private const string GET_INCOMING_PRIVATE_VOICE_CHAT_REQUEST = "GetIncomingPrivateVoiceChatRequest";

        private readonly IRPCSocialServices socialServiceRPC;
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private CancellationTokenSource subscriptionCts = new ();
        private bool isServiceDisabled;

        public RPCPrivateVoiceChatService(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus) : base(socialServiceRPC, ReportCategory.COMMUNITY_VOICE_CHAT)
        {
            this.socialServiceRPC = socialServiceRPC;
            this.socialServiceEventBus = socialServiceEventBus;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT))
            {
                socialServiceEventBus.TransportClosed += OnTransportClosed;
                socialServiceEventBus.RPCClientReconnected += OnTransportReconnected;
                socialServiceEventBus.WebSocketConnectionEstablished += OnTransportConnected;
            }
            else { isServiceDisabled = true; }
        }

        public override void Dispose()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT)) return;

            socialServiceEventBus.TransportClosed -= OnTransportClosed;
            socialServiceEventBus.RPCClientReconnected -= OnTransportReconnected;
            socialServiceEventBus.WebSocketConnectionEstablished -= OnTransportConnected;
            subscriptionCts.SafeCancelAndDispose();

            base.Dispose();
        }

        private void ThrowIfServiceDisabled()
        {
            if (isServiceDisabled) { throw new InvalidOperationException("Voice chat service is disabled."); }
        }

        private void OnTransportConnected()
        {
            if (!isServiceDisabled) { SubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget(); }
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
                SubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
            }
        }

        public async UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new StartPrivateVoiceChatPayload
            {
                Callee = new User
                {
                    Address = userId,
                },
            };

            StartPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                            .CallUnaryProcedure<StartPrivateVoiceChatResponse>(START_PRIVATE_VOICE_CHAT, payload)
                                                                            .AttachExternalCancellation(ct)
                                                                            .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new AcceptPrivateVoiceChatPayload
            {
                CallId = callId,
            };

            AcceptPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                             .CallUnaryProcedure<AcceptPrivateVoiceChatResponse>(ACCEPT_PRIVATE_VOICE_CHAT, payload)
                                                                             .AttachExternalCancellation(ct)
                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new RejectPrivateVoiceChatPayload
            {
                CallId = callId,
            };

            RejectPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                             .CallUnaryProcedure<RejectPrivateVoiceChatResponse>(REJECT_PRIVATE_VOICE_CHAT, payload)
                                                                             .AttachExternalCancellation(ct)
                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new EndPrivateVoiceChatPayload
            {
                CallId = callId,
            };

            EndPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                          .CallUnaryProcedure<EndPrivateVoiceChatResponse>(END_PRIVATE_VOICE_CHAT, payload)
                                                                          .AttachExternalCancellation(ct)
                                                                          .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<GetIncomingPrivateVoiceChatRequestResponse> GetIncomingPrivateVoiceChatRequestAsync(CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            GetIncomingPrivateVoiceChatRequestResponse? response = await socialServiceRPC.Module()!
                                                                                         .CallUnaryProcedure<GetIncomingPrivateVoiceChatRequestResponse>(GET_INCOMING_PRIVATE_VOICE_CHAT_REQUEST, new Empty())
                                                                                         .AttachExternalCancellation(ct)
                                                                                         .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                var retryAttempt = 0;
                var streamOpened = false;

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        IUniTaskAsyncEnumerable<PrivateVoiceChatUpdate> stream =
                            socialServiceRPC.Module()!.CallServerStream<PrivateVoiceChatUpdate>(SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES, new Empty());

                        streamOpened = true;
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Successfully opened private voice chat updates stream");

                        await foreach (PrivateVoiceChatUpdate? response in stream)
                        {
                            try { PrivateVoiceChatUpdateReceived?.Invoke(response); }

                            // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                            catch (Exception e) when (e is not OperationCanceledException)
                            {
                                DiagnosticInfoUtils.LogWebSocketException(e,ReportCategory.VOICE_CHAT);
                            }
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
                        ReportHub.LogError(new ReportData(ReportCategory.VOICE_CHAT), $"{TAG} Failed to open private voice chat updates stream (attempt {retryAttempt}/{MAX_STREAM_RETRY_ATTEMPTS} exception {e}");

                        if (retryAttempt >= MAX_STREAM_RETRY_ATTEMPTS)
                        {
                            ReportHub.LogError(new ReportData(ReportCategory.VOICE_CHAT), $"{TAG} Failed to open private voice chat updates stream after {MAX_STREAM_RETRY_ATTEMPTS} attempts. Disabling voice chat service.");
                            isServiceDisabled = true;
                            break;
                        }

                        // Calculate exponential backoff delay
                        int delaySeconds = BASE_RETRY_DELAY_SECONDS * (int)Math.Pow(2, retryAttempt - 1);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Retrying private voice chat updates stream connection in {delaySeconds} seconds...");

                        try { await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct); }
                        catch (OperationCanceledException)
                        {
                            // Cancellation requested during delay, exit the retry loop
                            break;
                        }
                    }
                }

                if (!streamOpened && !ct.IsCancellationRequested)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to establish private voice chat updates stream after all retry attempts");
                }
            }
        }
    }
}
