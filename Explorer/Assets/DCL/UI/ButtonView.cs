using DCL.Audio;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
        private TMP_Text text { get; set; }
        [field: SerializeField]
        private Color interactableColor = new Color(1f, 1f, 1f, 1f);
        [field: SerializeField]
        private Color hoverColor = new Color(0.54f, 0.54f, 0.54f);

        public event Action<PointerEventData>? OnMouseClickEvent;

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
            var color = isInteractable ? interactableColor : hoverColor;

            foreach (var image in images)
            {
                image.color = color;
            }

            text.color = color;
            Button.interactable = isInteractable;
        }

        public void SetText(string value) => text.text = value;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonHoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData) =>
            OnMouseClickEvent?.Invoke(eventData);
    }
}
