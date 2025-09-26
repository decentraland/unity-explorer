using Cysharp.Threading.Tasks;
using Decentraland.SocialService.V2;
using System;
using System.Threading;

namespace DCL.VoiceChat.Services
{
    public interface ICommunityVoiceService : IDisposable
    {
        /// <summary>
        /// Only needed by the CommunitiesStatusService, as it handles updates to communities call states.
        /// </summary>
        event Action<CommunityVoiceChatUpdate> CommunityVoiceChatUpdateReceived;

        event Action<ActiveCommunityVoiceChatsResponse> ActiveCommunityVoiceChatsFetched;

        UniTask<RequestToSpeakInCommunityVoiceChatResponse> RequestToSpeakInCommunityVoiceChatAsync(string communityId, bool isRequestingToSpeak, CancellationToken ct);

        UniTask<RejectSpeakRequestInCommunityVoiceChatResponse> DenySpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct);

        UniTask<PromoteSpeakerInCommunityVoiceChatResponse> PromoteSpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct);

        UniTask<DemoteSpeakerInCommunityVoiceChatResponse> DemoteSpeakerInCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct);

        UniTask<KickPlayerFromCommunityVoiceChatResponse> KickPlayerFromCommunityVoiceChatAsync(string communityId, string userAddress, CancellationToken ct);

        UniTask<StartCommunityVoiceChatResponse> StartCommunityVoiceChatAsync(string communityId, CancellationToken ct);

        UniTask<JoinCommunityVoiceChatResponse> JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct);

        UniTask<EndCommunityVoiceChatResponse> EndCommunityVoiceChatAsync(string communityId, CancellationToken ct);

        UniTask<ActiveCommunityVoiceChatsResponse> GetActiveCommunityVoiceChatsAsync(CancellationToken ct);

        UniTask SubscribeToCommunityVoiceChatUpdatesAsync(CancellationToken ct);
    }
}
