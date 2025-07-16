using DCL.Web3;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Interface for private voice chat call status service that exposes private call specific properties
    /// </summary>
    public interface IPrivateVoiceChatCallStatusService
    {
        Web3Address CurrentTargetWallet { get; }
    }
}
