using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class MicrophoneButton : MonoBehaviour
    {
        [field: SerializeField]
        public Button MicButton { get; private set; }

        [field: SerializeField]
        private Image MicImageField;

        [field: SerializeField]
        private Sprite MicOnImage;

        [field: SerializeField]
        private Sprite MicOffImage;

        public void SetMicrophoneStatus(bool isOn)
        {
            MicImageField.sprite = isOn ? MicOnImage : MicOffImage;
        }
    }
}
