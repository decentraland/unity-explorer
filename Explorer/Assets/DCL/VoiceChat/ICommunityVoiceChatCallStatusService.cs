using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat
{
    public interface ICommunityVoiceChatCallStatusService : IVoiceChatCallStatusServiceBase
    {
        bool HasActiveVoiceChatCall(string communityId);

        /// <summary>
        ///     Subscribes to updates for a specific community
        /// </summary>
        IReadonlyReactiveProperty<bool> CommunityConnectionUpdates(string communityId);

        bool TryGetActiveCommunityVoiceChat(string communityId, out ActiveCommunityVoiceChat activeCommunityVoiceChat);

        UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken cancellationToken = default);

        void RequestToSpeakInCurrentCall();

        void PromoteToSpeakerInCurrentCall(string walletId);

        void DemoteFromSpeakerInCurrentCall(string walletId);

        void KickPlayerFromCurrentCall(string walletId);
        void DenySpeakerInCurrentCall(string walletId);
        void LowerHandInCurrentCall();
        void EndStreamInCurrentCall();
    }
}
