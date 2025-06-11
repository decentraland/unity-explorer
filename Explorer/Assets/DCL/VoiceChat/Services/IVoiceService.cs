using Cysharp.Threading.Tasks;
using Decentraland.SocialService.V2;
using System;
using System.Threading;

namespace DCL.VoiceChat.Services
{
    public interface IVoiceService : IDisposable
    {
        UniTask<StartPrivateVoiceChatResponse> StartPrivateVoiceChatAsync(string userId, CancellationToken ct);

        UniTask<AcceptPrivateVoiceChatResponse> AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask<RejectPrivateVoiceChatResponse> RejectPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask<EndPrivateVoiceChatResponse> EndPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct);
    }
}
