using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using DCL.SocialService;
using System;
using System.Threading;
using Decentraland.SocialService.V2;
using Google.Protobuf.WellKnownTypes;

namespace DCL.VoiceChat.Services
{
    public class RPCVoiceChatService : IVoiceService
    {
        /// <summary>
        ///     Timeout used for foreground operations
        /// </summary>
        private const int FOREGROUND_TIMEOUT_SECONDS = 10;

        private const string START_PRIVATE_VOICE_CHAT = "StartPrivateVoiceChat";
        private const string ACCEPT_PRIVATE_VOICE_CHAT = "AcceptPrivateVoiceChat";
        private const string REJECT_PRIVATE_VOICE_CHAT = "RejectPrivateVoiceChat";
        private const string END_PRIVATE_VOICE_CHAT = "EndPrivateVoiceChat";
        private const string SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES = "SubscribeToPrivateVoiceChatUpdates";

        private readonly ISelfProfile selfProfile;
        private readonly IRPCSocialServices socialServiceRPC;

        public RPCVoiceChatService(
            ISelfProfile selfProfile,
            IRPCSocialServices socialServiceRPC)
        {
            this.selfProfile = selfProfile;
            this.socialServiceRPC = socialServiceRPC;
        }

        public void Dispose()
        {
        }

        public async UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct)
        {
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
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new RejectPrivateVoiceChatPayload
            {
                CallId = callId
            };

            RejectPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                             .CallUnaryProcedure<RejectPrivateVoiceChatResponse>(REJECT_PRIVATE_VOICE_CHAT, payload)
                                                                             .AttachExternalCancellation(ct)
                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public async UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new EndPrivateVoiceChatPayload
            {
                CallId = callId
            };

            EndPrivateVoiceChatResponse? response = await socialServiceRPC.Module()!
                                                                             .CallUnaryProcedure<EndPrivateVoiceChatResponse>(END_PRIVATE_VOICE_CHAT, payload)
                                                                             .AttachExternalCancellation(ct)
                                                                             .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response;
        }

        public UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<PrivateVoiceChatUpdate> stream =
                    socialServiceRPC.Module()!.CallServerStream<PrivateVoiceChatUpdate>(SUBSCRIBE_TO_PRIVATE_VOICE_CHAT_UPDATES, new Empty());

                await foreach (PrivateVoiceChatUpdate? response in stream)
                {
                    try
                    {
                        switch (response.Status)
                        {
                            case PrivateVoiceChatStatus.VoiceChatAccepted:
                                break;
                            case PrivateVoiceChatStatus.VoiceChatEnded:
                                break;
                            case PrivateVoiceChatStatus.VoiceChatExpired:
                                break;
                            case PrivateVoiceChatStatus.VoiceChatRejected:
                                break;
                            case PrivateVoiceChatStatus.VoiceChatRequested:
                                break;
                        }
                    }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.VOICE_CHAT)); }
                }
            }
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
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.VOICE_CHAT)); }
            }
        }
    }
}
