using Cysharp.Threading.Tasks;
using Decentraland.SocialService.V2;
using System;
using System.Threading;

namespace DCL.VoiceChat.Services
{
    public interface ICommunityVoiceService : IDisposable
    {
        event Action<CommunityVoiceChatUpdate> CommunityVoiceChatUpdateReceived;

        UniTask<StartCommunityVoiceChatResponse> StartCommunityVoiceChatAsync(string communityId, CancellationToken ct);

        UniTask<JoinCommunityVoiceChatResponse> JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct);

        UniTask SubscribeToCommunityVoiceChatUpdatesAsync(CancellationToken ct);
    }
}
