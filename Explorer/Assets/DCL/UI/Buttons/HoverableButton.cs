using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;

namespace DCL.UI.Buttons
{
    public class HoverableButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action? OnButtonHover;
        public event Action? OnButtonUnhover;

        [field: SerializeField] public Button Button { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig ButtonPressedAudio { get; private set; } = null!;
        [field: SerializeField] public AudioClipConfig ButtonHoveredAudio { get; private set; } = null!;

        public void Awake()
        {
            Button.onClick.AddListener(OnButtonPressed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonHoveredAudio);
            OnButtonHover?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData) =>
            OnButtonUnhover?.Invoke();

        private void OnButtonPressed()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ButtonPressedAudio);
        }
    }
}
