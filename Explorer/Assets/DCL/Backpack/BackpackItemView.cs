using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Backpack
{
    public class BackpackItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private readonly Vector3 hoveredScale = new (1.1f,1.1f,1.1f);
        private const float ANIMATION_TIME = 0.1f;

        public event Action<string>? OnSelectItem;
        public event Action<string>? OnEquip;
        public event Action<string>? OnUnequip;

        [field: SerializeField]
        public string ItemId { get; set; }

        [field: SerializeField]
        public RectTransform ContainerTransform { get; private set; }

        [field: SerializeField]
        public RectTransform HoverBackgroundTransform { get; private set; }

        [field: SerializeField]
        public Button EquipButton { get; private set; }

        [field: SerializeField]
        public Button UnEquipButton { get; private set; }

        [field: SerializeField]
        public GameObject EquippedIcon { get; private set; }

        [field: SerializeField]
        public Image CategoryImage { get; private set; }

        [field: SerializeField]
        public Image WearableThumbnail { get; private set; }

        [field: SerializeField]
        public Image RarityBackground { get; private set; }

        [field: SerializeField]
        public Image FlapBackground { get; private set; }

        [field: SerializeField]
        public LoadingBrightView LoadingView { get; private set; }

        [field: SerializeField]
        public GameObject FullBackpackItem { get; private set; }

        [field: SerializeField]
        public bool IsEquipped { get; set; }

        [SerializeField] private GameObject incompatibleWithBodyShapeContainer;
        [SerializeField] private GameObject incompatibleWithBodyShapeHoverContainer;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig EquipWearableAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig UnEquipWearableAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }

        public bool IsCompatibleWithBodyShape
        {
            get => isCompatibleWithBodyShape;

            set
            {
                incompatibleWithBodyShapeContainer.SetActive(!value);
                isCompatibleWithBodyShape = value;
            }
        }

        private CancellationTokenSource cts;
        private bool isCompatibleWithBodyShape;

        private void Awake()
        {
            EquipButton.onClick.AddListener(() =>
            {
                OnEquip?.Invoke(ItemId);
                UIAudioEventsBus.Instance.SendPlayAudioEvent(EquipWearableAudio);
            });

            UnEquipButton.onClick.AddListener(() =>
            {
                OnUnequip?.Invoke(ItemId);
                UIAudioEventsBus.Instance.SendPlayAudioEvent(UnEquipWearableAudio);
            });
        }

        public void SetEquipButtonsState()
        {
            EquipButton.gameObject.SetActive(!IsEquipped && IsCompatibleWithBodyShape);
            UnEquipButton.gameObject.SetActive(IsEquipped);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateHover();
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            SetEquipButtonsState();

            if (IsEquipped)
                EquippedIcon.gameObject.SetActive(false);

            incompatibleWithBodyShapeHoverContainer.SetActive(!IsCompatibleWithBodyShape);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateExit();
            EquippedIcon.gameObject.SetActive(IsEquipped);
            incompatibleWithBodyShapeHoverContainer.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(ItemId)) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            switch (eventData.clickCount)
            {
                case 1:
                    OnSelectItem?.Invoke(ItemId);
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
                    break;
                case 2:
                    if (IsCompatibleWithBodyShape)
                    {
                        OnEquip?.Invoke(ItemId);
                        UIAudioEventsBus.Instance.SendPlayAudioEvent(EquipWearableAudio);
                    }
                    break;
            }
        }

        private void AnimateHover()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            HoverBackgroundTransform.localScale = Vector3.zero;
            HoverBackgroundTransform.gameObject.SetActive(true);
            ContainerTransform.DOScale(hoveredScale, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
        }

        private void AnimateExit()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            ContainerTransform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            HoverBackgroundTransform.DOScale(Vector3.zero, ANIMATION_TIME).SetEase(Ease.Flash)
                                    .OnComplete(()=>HoverBackgroundTransform.gameObject.SetActive(false)).ToUniTask(cancellationToken: cts.Token);
        }
    }
}
