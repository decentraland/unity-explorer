using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatEventBus : IVoiceChatEventBus
    {
        public event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;

        public void BroadcastPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update) =>
            PrivateVoiceChatUpdateReceived?.Invoke(update);
    }
}
