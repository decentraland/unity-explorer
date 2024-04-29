using DCL.Audio;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.UI;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class AvatarSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float ANIMATION_TIME = 0.2f;
        public event Action<AvatarSlotView> OnSlotButtonPressed;

        [field: SerializeField]
        public bool BlockUnEquip { get; private set; }

        [field: SerializeField]
        internal Image focusedImage { get; private set; }

        [field: SerializeField]
        public string Category { get; private set; }

        [field: SerializeField]
        public Button SlotButton { get; private set; }

        [field: SerializeField]
        public GameObject EmptyOverlay { get; private set; }

        [field: SerializeField]
        public GameObject HoverTootlip { get; private set; }

        [field: SerializeField]
        public GameObject SelectedBackground { get; private set; }

        [field: SerializeField]
        public Button UnequipButton { get; private set; }

        [field: SerializeField]
        public GameObject OverrideHideContainer { get; private set; }

        [field: SerializeField]
        public Button OverrideHide { get; private set; }

        [field: SerializeField]
        public Button NoOverride { get; private set; }

        [field: SerializeField]
        public TMP_Text CategoryText { get; private set; }

        [field: SerializeField]
        public TMP_Text HiderText { get; private set; }

        [field: SerializeField]
        public string SlotWearableUrn { get; set; }

        [field: SerializeField]
        public Image SlotWearableThumbnail { get; set; }

        [field: SerializeField]
        public Image SlotWearableRarityBackground { get; set; }

        [field: SerializeField]
        public LoadingBrightView LoadingView { get; private set; }

        [field: SerializeField]
        public GameObject NftContainer { get; private set; }

        [field: SerializeField]
        public AvatarWearableCategoryEnum CategoryEnum;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig OverrideHideAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig DontOverrideHideAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig UnequipAudio { get; private set; }

        private void OnEnable()
        {
            OverrideHide.onClick.AddListener(PlayOverrideHideAudio);
            NoOverride.onClick.AddListener(PlayDontOverrideHideAudio);
            UnequipButton.onClick.AddListener(PlayUnequipAudio);
        }

        private void OnDisable()
        {
            OverrideHide.onClick.RemoveListener(PlayOverrideHideAudio);
            NoOverride.onClick.RemoveListener(PlayDontOverrideHideAudio);
            UnequipButton.onClick.RemoveListener(PlayUnequipAudio);

        }

        private void PlayOverrideHideAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(OverrideHideAudio);

        private void PlayDontOverrideHideAudio () =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(DontOverrideHideAudio);

        private void PlayUnequipAudio () =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(UnequipAudio);

        public void Start()
        {
            AvatarWearableHide.CATEGORIES_TO_READABLE.TryGetValue(Category.ToLower(), out string readableCategoryHider);
            CategoryText.text = readableCategoryHider;
            SlotButton.onClick.AddListener(InvokeSlotButtonPressed);
        }

        public void InvokeSlotButtonPressed()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
            OnSlotButtonPressed?.Invoke(this);
            ScaleUpAnimation(SelectedBackground.transform);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            HoverTootlip.SetActive(true);
            focusedImage.enabled = true;
            UnequipButton.gameObject.SetActive(!string.IsNullOrEmpty(SlotWearableUrn) && !BlockUnEquip);
            ScaleUpAnimation(focusedImage.transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTootlip.SetActive(false);
            UnequipButton.gameObject.SetActive(false);
            focusedImage.enabled = false;
        }

        private void ScaleUpAnimation(Transform targetTransform)
        {
            targetTransform.transform.localScale = new Vector3(0, 0, 0);
            targetTransform.transform.DOScale(1, ANIMATION_TIME).SetEase(Ease.OutBack);
        }

    }
}
