using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System.Threading;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Interface for community voice chat call status service that exposes community calls specific properties
    /// </summary>
    public interface ICommunityVoiceChatCallStatusService
    {
        /// <summary>
        /// Checks if a community has an active voice chat call
        /// </summary>
        /// <param name="communityId">The community ID to check</param>
        /// <param name="callId">The call ID if the community has an active call, null otherwise</param>
        bool HasActiveVoiceChatCall(string communityId, out string? callId);

        /// <summary>
        /// Subscribes to updates for a specific community
        /// </summary>
        /// <returns>A reactive property that will receive updates for this community, or null if already subscribed</returns>
        IReadonlyReactiveProperty<CommunityCallStatus>? SubscribeToCommunityUpdates(string communityId);

        void UnsubscribeFromCommunityUpdates(string communityId);

        UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the voice chat call status for a specific community
    /// </summary>
    public readonly struct CommunityCallStatus
    {
        public readonly string? VoiceChatId;
        public readonly bool HasActiveCall;

        public CommunityCallStatus(string? voiceChatId)
        {
            VoiceChatId = voiceChatId;
            HasActiveCall = !string.IsNullOrEmpty(voiceChatId);
        }

        public static CommunityCallStatus NoCall => new(null);
    }
}
