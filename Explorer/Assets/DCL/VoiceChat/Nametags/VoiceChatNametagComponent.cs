using UnityEngine;

namespace DCL.VoiceChat
{
    public struct VoiceChatNametagComponent
    {
        public readonly bool IsSpeaking;
        public readonly bool IsHushed;
        public bool IsDirty;
        public bool IsRemoving;

        public VoiceChatNametagComponent(bool isSpeaking, bool isHushed = false)
        {
            IsSpeaking = isSpeaking;
            IsHushed = isHushed;
            IsDirty = true;
            IsRemoving = false;
        }
    }
}
