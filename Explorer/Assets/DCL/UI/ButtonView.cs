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

        [field: Header("Interactable Properties")]
        [field: SerializeField]
        private Image[] images { get; set; }
        [field: SerializeField]
        private Color interactableColor = new Color(1f, 1f, 1f, 1f);
        [field: SerializeField]
        private Color hoverColor = new Color(0.54f, 0.54f, 0.54f);
        
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

        public void SetInteractable(bool isInteractable)
        {
            foreach (var image in images)
            {
                image.color = isInteractable ? interactableColor : hoverColor;
            }
            Button.interactable = isInteractable;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonHoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData) { }
    }
}
