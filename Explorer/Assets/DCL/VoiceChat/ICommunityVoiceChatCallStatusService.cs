using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Interface for community voice chat call status service that exposes community calls specific properties
    /// </summary>
    public interface ICommunityVoiceChatCallStatusService
    {
        /// <summary>
        ///     Checks if a community has an active voice chat call
        /// </summary>
        /// <param name="communityId">The community ID to check</param>
        bool HasActiveVoiceChatCall(string communityId);

        /// <summary>
        ///     Subscribes to updates for a specific community
        /// </summary>
        ReactiveProperty<bool> SubscribeToCommunityUpdates(string communityId);

        bool TryGetActiveCommunityVoiceChat(string communityId, out ActiveCommunityVoiceChat activeCommunityVoiceChat);

        UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken cancellationToken = default);

        void RequestToSpeakInCurrentCall();

        void PromoteToSpeakerInCurrentCall(string walletId);

        void DemoteFromSpeakerInCurrentCall(string walletId);

        void KickPlayerFromCurrentCall(string walletId);
    }
}
