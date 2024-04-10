using DCL.UI;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class EmoteSlotContainerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float ANIMATION_TIME = 0.2f;

        [field: SerializeField]
        internal Image FocusedImage { get; private set; }

        [field: SerializeField]
        public Image BackgroundRarity { get; private set; }

        [field: SerializeField]
        public TMP_Text EmoteName { get; private set; }

        [field: SerializeField]
        public LocalizeStringEvent EmptyEmoteName { get; private set; }

        [field: SerializeField]
        public GameObject SelectedBackground { get; private set; }

        [field: SerializeField]
        public Image SlotWearableThumbnail { get; set; }

        [field: SerializeField]
        public Button SlotButton { get; private set; }

        [field: SerializeField]
        public GameObject EmptyOverlay { get; private set; }

        [field: SerializeField]
        public LoadingBrightView LoadingView { get; private set; }

        [field: SerializeField]
        public GameObject NftContainer { get; private set; }

        [field: SerializeField]
        public Button UnEquipButton { get; private set; }

        public event Action<EmoteSlotContainerView>? OnSlotButtonPressed;

        private void Start()
        {
            SlotButton.onClick.AddListener(OnButtonPressed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UnEquipButton.gameObject.SetActive(!EmptyOverlay.activeSelf);
            FocusedImage.enabled = true;
            ScaleUpAnimation(FocusedImage.transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            FocusedImage.enabled = false;
            UnEquipButton.gameObject.SetActive(false);
        }

        public void StartLoadingAnimation()
        {
            LoadingView.StartLoadingAnimation(NftContainer);
        }

        private void ScaleUpAnimation(Transform targetTransform)
        {
            targetTransform.localScale = new Vector3(0, 0, 0);
            targetTransform.DOScale(1, ANIMATION_TIME).SetEase(Ease.OutBack);
        }

        private void OnButtonPressed()
        {
            OnSlotButtonPressed?.Invoke(this);
            ScaleUpAnimation(SelectedBackground.transform);
        }
    }
}
