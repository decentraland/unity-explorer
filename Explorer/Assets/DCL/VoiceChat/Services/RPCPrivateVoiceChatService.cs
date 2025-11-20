using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.SocialService;
using System;
using System.Threading;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
using Sentry;
using System.Net.WebSockets;
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

        private readonly ISocialServiceEventBus socialServiceEventBus;
        private CancellationTokenSource subscriptionCts = new ();
        private bool isServiceDisabled;

        public RPCPrivateVoiceChatService(
            IRPCSocialServices socialServiceRPC,
            ISocialServiceEventBus socialServiceEventBus) : base(socialServiceRPC, ReportCategory.COMMUNITY_VOICE_CHAT)
        {
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
            if (isServiceDisabled)
                throw new InvalidOperationException("Voice chat service is disabled.");
        }

        private void OnTransportConnected()
        {
            if (!isServiceDisabled)
                SubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
        }

        private void OnTransportClosed()
        {
            subscriptionCts = subscriptionCts.SafeRestart();
            Disconnected?.Invoke();
        }

        private void OnTransportReconnected()
        {
            if (isServiceDisabled) return;
            Reconnected?.Invoke();
            SubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
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

        private async UniTaskVoid SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct)
        {
            await KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);
            return;

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
                            // It is not needed to handle OperationCancelledException nor WebSocketException because it is an internal sync call
                            catch (Exception e) { ReportHub.LogException(e, ReportCategory.VOICE_CHAT); }
                        }

                        // If we reach here, the stream has ended normally
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested, exit the retry loop
                        break;
                    }
                    catch (WebSocketException e)
                    {
                        retryAttempt++;

                        SentrySdk.AddBreadcrumb($"WebSocketException reason was WebSocketErrorCode: {e.WebSocketErrorCode.ToString()} "
                                                + $"ErrorCode: {e.ErrorCode.ToString()}", ReportCategory.VOICE_CHAT, level: BreadcrumbLevel.Info);

                        var webSocketErrorCode = (WebSocketError)e.ErrorCode;

                        if (webSocketErrorCode is WebSocketError.Faulted
                            or WebSocketError.ConnectionClosedPrematurely
                            or WebSocketError.NativeError
                            or WebSocketError.HeaderError)
                        {
                            if (!await TryRetryAsync(retryAttempt, ct))
                            {
                                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                                break;
                            }

                            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "WebSocketException occurred while getting private voice chat updates, retrying..");
                        }
                        else
                        {
                            ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        retryAttempt++;

                        ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to open private voice chat updates stream (attempt {retryAttempt}/{MAX_STREAM_RETRY_ATTEMPTS} exception {e}");

                        if (!await TryRetryAsync(retryAttempt, ct))
                        {
                            ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                            isServiceDisabled = true;
                            break;
                        }
                    }
                }

                if (!streamOpened && !ct.IsCancellationRequested) { ReportHub.LogError(ReportCategory.VOICE_CHAT, $"{TAG} Failed to establish private voice chat updates stream after all retry attempts"); }
            }

            async UniTask<bool> TryRetryAsync(int retryAttempt, CancellationToken ct)
            {
                if (retryAttempt >= MAX_STREAM_RETRY_ATTEMPTS)
                {
                    ReportHub.LogError(new ReportData(ReportCategory.VOICE_CHAT), $"{TAG} Failed to open private voice chat updates stream after {MAX_STREAM_RETRY_ATTEMPTS} attempts. Disabling voice chat service.");
                    return false;
                }

                // Calculate exponential backoff delay
                int delaySeconds = BASE_RETRY_DELAY_SECONDS * (int)Math.Pow(2, retryAttempt - 1);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Retrying private voice chat updates stream connection in {delaySeconds} seconds...");

                try { await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct); }
                catch (OperationCanceledException) { return false; }

                return true;
            }
        }
    }
}
