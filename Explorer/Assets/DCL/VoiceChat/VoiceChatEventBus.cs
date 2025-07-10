using DCL.Web3;

namespace DCL.VoiceChat
{
    public class VoiceChatEventBus
    {
        public delegate void StartVoiceChatRequestedDelegate(Web3Address walletId);

        public event StartVoiceChatRequestedDelegate StartPrivateVoiceChatRequested;

        public void RequestStartPrivateVoiceChat(Web3Address walletId)
        {
            StartPrivateVoiceChatRequested?.Invoke(walletId);
        }
    }
}
