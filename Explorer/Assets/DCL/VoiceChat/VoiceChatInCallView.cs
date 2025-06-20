using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class VoiceChatInCallView : MonoBehaviour
    {
        [field: SerializeField]
        public MicrophoneButton MicrophoneButton;

        [field: SerializeField]
        public Button HangUpButton;

        [field: SerializeField]
        public Button ExpandButton;

        [field: SerializeField]
        public SimpleProfileView ProfileView;

        [field: SerializeField]
        public RectTransform NoPlayerTalking;

        [field: SerializeField]
        public RectTransform PeopleTalkingContainer;

        [field: SerializeField]
        public TMP_Text MultiplePeopleTalking;

        [field: SerializeField]
        public TMP_Text PlayerNameTalking;

        [field: SerializeField]
        public RectTransform isSpeakingIconRect;

        [field: SerializeField]
        public RectTransform isSpeakingIconOuterRect;
    }
}
