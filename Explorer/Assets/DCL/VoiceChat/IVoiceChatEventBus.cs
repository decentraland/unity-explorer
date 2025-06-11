using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatEventBus
    {
        event Action<PrivateVoiceChatUpdate> PrivateVoiceChatUpdateReceived;

        void BroadcastPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update);
    }
}
