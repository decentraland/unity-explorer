using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ButtonView : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set;}

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio;

        private void OnEnable()
        {
            Button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            Button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            AudioEventsBus.Instance.SendPlayAudioEvent(ButtonPressedAudio);
        }
    }
}
