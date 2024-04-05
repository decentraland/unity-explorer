using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class MultiStateButtonView : MonoBehaviour
    {
        [field: SerializeField]
        internal Button button { get; private set; }

        [field: SerializeField]
        internal GameObject buttonImageFill { get; private set; }

        [field: SerializeField]
        internal GameObject buttonImageOutline { get; private set; }

        [Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio;

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            AudioEventsBus.Instance.SendPlayAudioEvent(ButtonPressedAudio);
        }

    }
}
