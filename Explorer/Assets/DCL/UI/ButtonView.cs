using DCL.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public Button Button { get; private set;}

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ButtonHoverAudio { get; private set; }

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
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonPressedAudio);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonHoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData) { }
    }
}
