using DCL.Audio;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.FacialExpressionsWheel
{
    /// <summary>
    ///     One slot around the facial expressions wheel. Mirrors <c>EmoteWheelSlotView</c> but
    ///     without the rarity background, since face expressions have no tier system.
    /// </summary>
    public class FacialExpressionWheelSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<int>? OnPlay;
        public event Action<int>? OnHover;
        public event Action<int>? OnFocusLeave;

        [SerializeField]
        private Button playButton = null!;

        [field: SerializeField]
        public Image Icon { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text SlotLabel { get; private set; } = null!;

        [SerializeField]
        private GameObject hoverBackground = null!;

        [SerializeField]
        private GameObject selectedBackground = null!;

        [field: SerializeField]
        public Animator SlotAnimator { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; } = null!;

        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; } = null!;

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
            selectedBackground.SetActive(false);
        }

        public void SetSelected(bool selected) =>
            selectedBackground.SetActive(selected);

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            hoverBackground.SetActive(true);
            SlotAnimator.SetTrigger(UIAnimationHashes.HOVER);
            OnHover?.Invoke(Slot);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverBackground.SetActive(false);
            SlotAnimator.SetTrigger(UIAnimationHashes.UNHOVER);
            OnFocusLeave?.Invoke(Slot);
        }
    }
}