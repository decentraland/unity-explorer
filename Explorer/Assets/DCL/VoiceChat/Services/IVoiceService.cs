using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.VoiceChat.Services
{
    public interface IVoiceService : IDisposable
    {
        UniTask StartPrivateVoiceChatAsync(string userId, CancellationToken ct);

        UniTask AcceptPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask RejectPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask EndPrivateVoiceChatAsync(string callId, CancellationToken ct);

        UniTask SubscribeToPrivateVoiceChatUpdatesAsync(CancellationToken ct);
    }
}
