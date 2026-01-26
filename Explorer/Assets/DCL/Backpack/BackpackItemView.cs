using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
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

        public event Action<int, string>? OnSelectItem;
        public event Action<int, string>? OnEquip;
        public event Action<int, string>? OnUnequip;

        public int Slot { get; set; }

        [field: SerializeField]
        public string ItemId { get; set; }

        [field: SerializeField]
        public RectTransform ContainerTransform { get; private set; }

        [field: SerializeField]
        public RectTransform HoverBackgroundTransform { get; private set; }

        [field: SerializeField]
        public Button EquipButton { get; private set; }

        [field: SerializeField]
        public GameObject EquipSpinner { get; private set; }

        [field: SerializeField]
        public GameObject EquipButtonText { get; private set; }

        [field: SerializeField]
        public Button UnEquipButton { get; private set; }

        [field: SerializeField]
        public GameObject EquippedIcon { get; private set; }

        [field: SerializeField]
        public GameObject NewTag { get; private set; }

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

        public bool IsUnequippable { get; set; }

        [SerializeField] private GameObject incompatibleWithBodyShapeContainer;
        [SerializeField] private GameObject incompatibleWithBodyShapeHoverContainer;
        
        [Header("Pending Transfer")]
        [SerializeField] private GameObject pendingTransferContainer;
        [SerializeField] private GameObject pendingTransferHoverContainer;
        
        [Header("NFT Count")]
        [SerializeField] private GameObject nftCountContainer;
        [SerializeField] private TextMeshProUGUI nftCountText;

        [field: SerializeField]
        public GameObject SmartWearableBadgeContainer { get; private set; }

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
        
        public bool IsPendingTransfer
        {
            get => isPendingTransfer;

            set
            {
                isPendingTransfer = value;
                if (pendingTransferContainer != null)
                    pendingTransferContainer.SetActive(value);
            }
        }
        
        public int NftCount
        {
            set
            {
                if (nftCountContainer != null)
                {
                    bool showCount = value > 1;
                    nftCountContainer.SetActive(showCount);
                    if (showCount && nftCountText != null)
                        nftCountText.text = $"x{value}";
                }
            }
        }

        public bool CanHover { get; set; } = true;

        public bool IsLoading
        {
            get => isLoading;

            set
            {
                isLoading = value;
                EquipButtonText.SetActive(!isLoading);
                EquipSpinner.SetActive(isLoading);
            }
        }
        private bool isLoading;

        private CancellationTokenSource cts;
        private bool isCompatibleWithBodyShape;
        private bool isPendingTransfer;

        private void Awake()
        {
            EquipButton.onClick.AddListener(() =>
            {
                if (IsLoading) return;

                OnEquip?.Invoke(Slot, ItemId);
                UIAudioEventsBus.Instance.SendPlayAudioEvent(EquipWearableAudio);
            });

            UnEquipButton.onClick.AddListener(() =>
            {
                OnUnequip?.Invoke(Slot, ItemId);
                UIAudioEventsBus.Instance.SendPlayAudioEvent(UnEquipWearableAudio);
            });
        }

        public void SetEquipButtonsState()
        {
            EquipButton.gameObject.SetActive(!IsEquipped && IsCompatibleWithBodyShape && !IsPendingTransfer);
            UnEquipButton.gameObject.SetActive(IsEquipped && IsUnequippable && !IsPendingTransfer);
        }

        private void OnDisable()
        {
            ContainerTransform.localScale = Vector3.one;
            HoverBackgroundTransform.localScale = Vector3.zero;
            HoverBackgroundTransform.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanHover) return;

            AnimateHover();
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            SetEquipButtonsState();

            if (IsEquipped)
                EquippedIcon.gameObject.SetActive(false);

            incompatibleWithBodyShapeHoverContainer.SetActive(!IsCompatibleWithBodyShape);
            
            if (pendingTransferHoverContainer != null)
                pendingTransferHoverContainer.SetActive(IsPendingTransfer);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateExit();
            EquippedIcon.gameObject.SetActive(IsEquipped);
            incompatibleWithBodyShapeHoverContainer.SetActive(false);
            
            if (pendingTransferHoverContainer != null)
                pendingTransferHoverContainer.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(ItemId)) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (!CanHover && eventData.clickCount != 2) return;

            switch (eventData.clickCount)
            {
                case 1:
                    OnSelectItem?.Invoke(Slot, ItemId);
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
                    break;
                case 2:
                    if (IsCompatibleWithBodyShape && !IsPendingTransfer)
                    {
                        OnEquip?.Invoke(Slot, ItemId);
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
