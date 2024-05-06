using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.EmotesWheel
{
    public class EmoteWheelSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int HOVER = Animator.StringToHash("Hover");
        private static readonly int UNHOVER = Animator.StringToHash("Unhover");

        public event Action<int>? OnPlay;
        public event Action<int>? OnHover;
        public event Action<int>? OnFocusLeave;

        [SerializeField]
        private Button playButton;

        [field: SerializeField]
        public Image BackgroundRarity { get; private set; }

        [SerializeField]
        private GameObject hoverBackground;

        [field: SerializeField]
        public Image Thumbnail { get; set; }

        [field: SerializeField]
        public GameObject EmptyContainer { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }

        [field: SerializeField]
        public Animator SlotAnimator { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        public int Slot { get; set; }

        private void Awake()
        {
            playButton.onClick.AddListener(() =>
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
                OnPlay?.Invoke(Slot);
            });
        }

        private void OnEnable()
        {
            SlotAnimator.Rebind();
            SlotAnimator.Update(0);
        }

        private void OnDisable()
        {
            hoverBackground.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            hoverBackground.SetActive(true);
            SlotAnimator.SetTrigger(HOVER);
            OnHover?.Invoke(Slot);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverBackground.SetActive(false);
            SlotAnimator.SetTrigger(UNHOVER);
            OnFocusLeave?.Invoke(Slot);
        }
    }
}
