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
        private const string START_PRIVATE_VOICE_CHAT = "StartPrivateVoiceChat";
        private const string ACCEPT_PRIVATE_VOICE_CHAT = "AcceptPrivateVoiceChat";
        private const string REJECT_PRIVATE_VOICE_CHAT = "RejectPrivateVoiceChat";
        private const string END_PRIVATE_VOICE_CHAT = "EndPrivateVoiceChat";
        private const string SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES = "SubscribeToPrivateVoiceChatUpdates";
        private const string GET_INCOMING_PRIVATE_VOICE_CHAT_REQUEST = "GetIncomingPrivateVoiceChatRequest";

        private readonly ISocialServiceEventBus socialServiceEventBus;
        private CancellationTokenSource subscriptionCts = new ();
        private bool isServiceDisabled;
        private bool isListeningUpdatesFromServer;

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
                TrySubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
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
            TrySubscribeToPrivateVoiceChatUpdatesAsync(subscriptionCts.Token).Forget();
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

        private async UniTaskVoid TrySubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct)
        {
            if (isListeningUpdatesFromServer) return;

            try
            {
                isListeningUpdatesFromServer = true;
                await KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);
            }
            finally { isListeningUpdatesFromServer = false; }

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<PrivateVoiceChatUpdate> stream =
                    socialServiceRPC.Module().CallServerStream<PrivateVoiceChatUpdate>(SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES, new Empty());

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Successfully opened private voice chat updates stream");

                await foreach (PrivateVoiceChatUpdate? response in stream)
                {
                    try { PrivateVoiceChatUpdateReceived?.Invoke(response); }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    // It is not needed to handle OperationCancelledException nor WebSocketException because it is an internal sync call
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.VOICE_CHAT); }
                }
            }
        }
    }
}
