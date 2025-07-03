using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.SocialService;
using System;
using System.Threading;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;
using Utility;

namespace DCL.VoiceChat.Services
{
    public class RPCVoiceChatService : IVoiceService
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

        private const string START_PRIVATE_VOICE_CHAT = "StartPrivateVoiceChat";
        private const string ACCEPT_PRIVATE_VOICE_CHAT = "AcceptPrivateVoiceChat";
        private const string REJECT_PRIVATE_VOICE_CHAT = "RejectPrivateVoiceChat";
        private const string END_PRIVATE_VOICE_CHAT = "EndPrivateVoiceChat";
        private const string SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES = "SubscribeToPrivateVoiceChatUpdates";
        private const string GET_INCOMING_PRIVATE_VOICE_CHAT_REQUEST = "GetIncomingPrivateVoiceChatRequest";

        private readonly IRPCSocialServices socialServiceRPC;
        private readonly CancellationTokenSource subscriptionCts = new ();
        private CancellationTokenSource streamCts = new ();

        private bool isServiceDisabled = true;
        private bool isStreamConnected;
        private ConnectionSubscription connectionSubscription;

        public event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;
        public event Action Connected;
        public event Action Disconnected;

        public RPCVoiceChatService(
            IRPCSocialServices socialServiceRPC)
        {
            this.socialServiceRPC = socialServiceRPC;

            connectionSubscription = socialServiceRPC.SubscribeToConnection(subscriptionCts.Token);
            connectionSubscription.Connected += OnConnectionEstablished;
            connectionSubscription.Disconnected += OnConnectionLost;
            connectionSubscription.ConnectionFailed += OnConnectionFailed;
        }

        public void Dispose()
        {
            connectionSubscription?.Dispose();
            connectionSubscription = null;

            streamCts.SafeCancelAndDispose();
            subscriptionCts.SafeCancelAndDispose();
        }

        private void ThrowIfServiceDisabled()
        {
            if (isServiceDisabled) { throw new InvalidOperationException("Voice chat service is disabled due to connection failures."); }
        }

        private async UniTask EnsureConnectionAsync(CancellationToken ct)
        {
            bool connected = await connectionSubscription!.WaitForConnectionAsync(ct);
            if (!connected)
            {
                throw new InvalidOperationException("Failed to establish connection within timeout period");
            }
        }

        private void OnConnectionEstablished()
        {
            // Re-enable service if it was disabled due to connection failures
            if (isServiceDisabled)
            {
                isServiceDisabled = false;
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Voice chat service re-enabled - connection restored");
            }

            Connected?.Invoke();
            StartVoiceChatStream();
        }

        private void OnConnectionLost()
        {
            isStreamConnected = false;
            StopVoiceChatStream();
            Disconnected?.Invoke();
        }

        private void OnConnectionFailed()
        {
            // Connection attempts exhausted - disable the service
            isServiceDisabled = true;
            isStreamConnected = false;
            StopVoiceChatStream();
            ReportHub.LogError(ReportCategory.VOICE_CHAT, "Voice chat service disabled due to connection failures");
            Disconnected?.Invoke();
        }

        private void StartVoiceChatStream()
        {
            streamCts = streamCts.SafeRestart();
            SubscribeToPrivateVoiceChatUpdatesAsync(streamCts.Token).Forget();
        }

        private void StopVoiceChatStream()
        {
            streamCts = streamCts.SafeRestart();
        }

        public async UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            try
            {
                await EnsureConnectionAsync(ct);

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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                throw new InvalidOperationException($"Failed to start private voice chat: {e.Message}", e);
            }
        }

        public async UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            try
            {
                await EnsureConnectionAsync(ct);

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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                throw new InvalidOperationException($"Failed to accept private voice chat: {e.Message}", e);
            }
        }

        public async UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            try
            {
                await EnsureConnectionAsync(ct);

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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                throw new InvalidOperationException($"Failed to reject private voice chat: {e.Message}", e);
            }
        }

        public async UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            try
            {
                await EnsureConnectionAsync(ct);

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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                throw new InvalidOperationException($"Failed to end private voice chat: {e.Message}", e);
            }
        }

        public async UniTask<GetIncomingPrivateVoiceChatRequestResponse> GetIncomingPrivateVoiceChatRequestAsync(CancellationToken ct)
        {
            ThrowIfServiceDisabled();

            try
            {
                await EnsureConnectionAsync(ct);

                GetIncomingPrivateVoiceChatRequestResponse? response = await socialServiceRPC.Module()!
                                                                                             .CallUnaryProcedure<GetIncomingPrivateVoiceChatRequestResponse>(GET_INCOMING_PRIVATE_VOICE_CHAT_REQUEST, new Empty())
                                                                                             .AttachExternalCancellation(ct)
                                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

                return response;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, ReportCategory.VOICE_CHAT);
                throw new InvalidOperationException($"Failed to get incoming private voice chat request: {e.Message}", e);
            }
        }

        public UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct)
        {
            return OpenStreamAndProcessUpdatesAsync();

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                var retryAttempt = 0;
                var streamOpened = false;

                while (retryAttempt < MAX_STREAM_RETRY_ATTEMPTS && !ct.IsCancellationRequested)
                {
                    try
                    {
                        await EnsureConnectionAsync(ct);

                        IUniTaskAsyncEnumerable<PrivateVoiceChatUpdate> stream =
                            socialServiceRPC.Module()!.CallServerStream<PrivateVoiceChatUpdate>(SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES, new Empty());

                        streamOpened = true;
                        isStreamConnected = true;

                        if (isStreamConnected && isServiceDisabled)
                        {
                            isServiceDisabled = false;
                            ReportHub.Log(ReportCategory.VOICE_CHAT, "Voice chat service enabled - stream connection established");
                        }

                        ReportHub.Log(ReportCategory.VOICE_CHAT, "Successfully opened private voice chat updates stream");

                        await foreach (PrivateVoiceChatUpdate? response in stream)
                        {
                            try { PrivateVoiceChatUpdateReceived?.Invoke(response); }
                            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.VOICE_CHAT); }
                        }

                        // If we reach here, the stream has ended normally
                        isStreamConnected = false;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested, exit the retry loop
                        isStreamConnected = false;
                        break;
                    }
                    catch (Exception e)
                    {
                        retryAttempt++;
                        isStreamConnected = false;
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"Failed to open private voice chat updates stream (attempt {retryAttempt}/{MAX_STREAM_RETRY_ATTEMPTS} exception {e}");

                        if (retryAttempt >= MAX_STREAM_RETRY_ATTEMPTS)
                        {
                            ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Failed to open private voice chat updates stream after {MAX_STREAM_RETRY_ATTEMPTS} attempts. Disabling voice chat service.");
                            isServiceDisabled = true;
                            break;
                        }

                        // Calculate exponential backoff delay
                        int delaySeconds = BASE_RETRY_DELAY_SECONDS * (int)Math.Pow(2, retryAttempt - 1);
                        ReportHub.Log(ReportCategory.VOICE_CHAT, $"Retrying private voice chat updates stream connection in {delaySeconds} seconds...");

                        try { await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct); }
                        catch (OperationCanceledException)
                        {
                            // Cancellation requested during delay, exit the retry loop
                            break;
                        }
                    }
                }

                if (!streamOpened && !ct.IsCancellationRequested) { ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to establish private voice chat updates stream after all retry attempts"); }
            }
        }
    }
}
