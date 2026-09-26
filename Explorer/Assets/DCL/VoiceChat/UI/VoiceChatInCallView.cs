using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class VoiceChatInCallView : MonoBehaviour
    {
        [SerializeField]
        public MicrophoneButton MicrophoneButton;

        [SerializeField]
        public Button HangUpButton;

        [SerializeField]
        public Button ExpandButton;

        [SerializeField]
        public SimpleProfileView ProfileView;

        [SerializeField]
        public RectTransform NoPlayerTalking;

        [SerializeField]
        public RectTransform PeopleTalkingContainer;

        [SerializeField]
        public TMP_Text MultiplePeopleTalking;

        [SerializeField]
        public TMP_Text PlayerNameTalking;

        [SerializeField]
        public RectTransform isSpeakingIconRect;

        [SerializeField]
        public RectTransform isSpeakingIconOuterRect;
    }
}
