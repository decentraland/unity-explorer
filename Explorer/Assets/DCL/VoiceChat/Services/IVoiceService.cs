using Cysharp.Threading.Tasks;
using Decentraland.SocialService.V2;
using System;
using System.Threading;

namespace DCL.VoiceChat.Services
{
    public interface IVoiceService : IDisposable
    {
        event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;
        event Action Reconnected;
        event Action Disconnected;

        UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct);

        UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct);

        UniTask<GetIncomingPrivateVoiceChatRequestResponse> GetIncomingPrivateVoiceChatRequestAsync(CancellationToken ct);

        class Null : IVoiceService
        {
            public static readonly Null INSTANCE = new();

            protected Null() { }

            public event Action<PrivateVoiceChatUpdate>? PrivateVoiceChatUpdateReceived;
            public event Action? Reconnected;
            public event Action? Disconnected;

            public UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct) =>
                UniTask.FromResult(new StartPrivateVoiceChatResponse());

            public UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct) =>
                UniTask.FromResult(new AcceptPrivateVoiceChatResponse());

            public UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct) =>
                UniTask.FromResult(new RejectPrivateVoiceChatResponse());

            public UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct) =>
                UniTask.FromResult(new EndPrivateVoiceChatResponse());

            public UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public UniTask<GetIncomingPrivateVoiceChatRequestResponse> GetIncomingPrivateVoiceChatRequestAsync(CancellationToken ct) =>
                UniTask.FromResult(new GetIncomingPrivateVoiceChatRequestResponse());

            public void Dispose() { }
        }
    }
}
