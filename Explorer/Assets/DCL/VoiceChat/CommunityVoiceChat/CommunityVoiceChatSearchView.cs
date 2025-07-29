using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text ListenersCounter { get; private set; }

        [field: SerializeField]
        public TMP_Text RequestToSpeakCounter { get; private set; }

        [field: SerializeField]
        public RectTransform RequestToSpeakSection { get; private set; }

        [field: SerializeField]
        public RectTransform RequestToSpeakParent { get; private set; }

        [field: SerializeField]
        public RectTransform ListenersParent { get; private set; }

        [field: SerializeField]
        public Button BackButton  { get; private set; }
    }
}
