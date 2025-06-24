using Cysharp.Threading.Tasks;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using Segment.Serialization;
using System;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class VoiceChatServiceAnalyticsDecorator : IVoiceService
    {
        public event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;
        public event Action Reconnected;
        public event Action Disconnected;

        private readonly IVoiceService core;
        private readonly IAnalyticsController analytics;

        public VoiceChatServiceAnalyticsDecorator(IVoiceService core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;

            core.PrivateVoiceChatUpdateReceived += c => PrivateVoiceChatUpdateReceived?.Invoke(c);
            core.Reconnected += () => Reconnected?.Invoke();
            core.Disconnected += () => Disconnected?.Invoke();
        }

        public async UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct)
        {
            StartPrivateVoiceChatResponse response = await core.StartPrivateVoiceChatAsync(userId, ct);

            analytics.Track(AnalyticsEvents.VoiceChat.START_CALL, new JsonObject
            {
                {AnalyticsEvents.VoiceChat.VOICE_CHAT_FRIENDSHIP_STATUS, true}
            });

            return response;
        }

        public async UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            AcceptPrivateVoiceChatResponse response = await core.AcceptPrivateVoiceChatAsync(callId, ct);
            analytics.Track(AnalyticsEvents.VoiceChat.ACCEPT_CALL);

            return response;
        }

        public async UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct)
        {
            RejectPrivateVoiceChatResponse response = await core.RejectPrivateVoiceChatAsync(callId, ct);
            analytics.Track(AnalyticsEvents.VoiceChat.REJECT_CALL);

            return response;
        }

        public UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct) =>
            core.EndPrivateVoiceChatAsync(callId, ct);

        public UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct) =>
            core.SubscribeToPrivateVoiceChatUpdatesAsync(ct);

        public UniTask<GetIncomingPrivateVoiceChatRequestResponse> GetIncomingPrivateVoiceChatRequestAsync(CancellationToken ct) =>
            core.GetIncomingPrivateVoiceChatRequestAsync(ct);

        public void Dispose()
        {
            core?.Dispose();
        }
    }
}
