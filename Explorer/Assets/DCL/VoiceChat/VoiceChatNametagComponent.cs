using UnityEngine;

namespace DCL.VoiceChat
{
    public struct VoiceChatNametagComponent
    {
        public readonly bool IsSpeaking;
        public bool IsDirty;
        public bool IsRemoving;

        public VoiceChatNametagComponent(bool isSpeaking)
        {
            IsSpeaking = isSpeaking;
            IsDirty = true;
            IsRemoving = false;
        }
    }
}
