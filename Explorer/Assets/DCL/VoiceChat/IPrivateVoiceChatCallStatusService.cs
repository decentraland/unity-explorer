using DCL.Web3;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Interface for private voice chat call status service that exposes private call specific properties
    /// </summary>
    public interface IPrivateVoiceChatCallStatusService : IVoiceChatCallStatusServiceBase
    {
        string CurrentTargetWallet { get; }
        event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;

        void AcceptCall();
        void RejectCall();
        void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update);
    }
}
