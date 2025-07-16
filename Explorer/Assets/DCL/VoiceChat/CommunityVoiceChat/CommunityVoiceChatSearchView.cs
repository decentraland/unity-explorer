using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform RequestToSpeakParent { get; private set; }

        [field: SerializeField]
        public RectTransform ListenersParent { get; private set; }
    }
}
