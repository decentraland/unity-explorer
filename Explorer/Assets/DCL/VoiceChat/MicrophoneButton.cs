using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class MicrophoneButton : MonoBehaviour
    {
        private const string MUTE_TEXT = "Mute";
        private const string UNMUTE_TEXT = "Unmute";

        [field: SerializeField]
        public Button MicButton { get; private set; }

        [field: SerializeField]
        private Image MicImageField;

        [field: SerializeField]
        private GameObject MicOnIcon;

        [field: SerializeField]
        private Sprite MicOnImage;

        [field: SerializeField]
        private Sprite MicOffImage;

        [field: SerializeField]
        private Color MicOnColor;

        [field: SerializeField]
        private Color MicOffColor;

        [field: SerializeField]
        private TMP_Text TooltipText;

        public void SetMicrophoneStatus(bool isOn)
        {
            MicImageField.sprite = isOn ? MicOnImage : MicOffImage;
            MicImageField.color = isOn ? MicOnColor : MicOffColor;
            MicOnIcon.SetActive(isOn);
            TooltipText.text = isOn ? MUTE_TEXT : UNMUTE_TEXT;
        }
    }
}
