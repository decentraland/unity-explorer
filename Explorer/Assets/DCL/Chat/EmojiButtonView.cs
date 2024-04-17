using DCL.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class EmojiButtonView : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set;}

        [field: SerializeField]
        public Image OffSprite { get; private set;}

        [field: SerializeField]
        public Image OnSprite { get; private set;}

        [field: SerializeField]
        private Color unfocusedColor;

        [field: SerializeField]
        private Color focusedColor;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ButtonPressedAudio { get; private set; }

        public void SetState(bool isOn)
        {
            OffSprite.gameObject.SetActive(!isOn);
            OnSprite.gameObject.SetActive(isOn);
        }

        public void SetColor(bool isTextboxFocused)
        {
            OffSprite.color = isTextboxFocused ? focusedColor : unfocusedColor;
        }

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
    }
}
