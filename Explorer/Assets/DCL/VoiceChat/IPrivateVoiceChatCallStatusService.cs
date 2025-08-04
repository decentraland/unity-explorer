using DCL.Web3;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Interface for private voice chat call status service that exposes private call specific properties
    /// </summary>
    public interface IPrivateVoiceChatCallStatusService
    {
        string CurrentTargetWallet { get; }
        
        event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;
    }
}
