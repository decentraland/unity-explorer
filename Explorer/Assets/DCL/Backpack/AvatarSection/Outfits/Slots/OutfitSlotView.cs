using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Backpack.AvatarSection.Outfits.Slots
{
    public class OutfitSlotView : MonoBehaviour
    {
        private readonly Vector3 hoveredScale = new (1.02f, 1.02f, 1.02f);
        private const float ANIMATION_TIME = 0.1f;
        private CancellationTokenSource cts;
        
        public event Action? OnSaveClicked;
        public event Action? OnEquipClicked;
        public event Action? OnDeleteClicked;
        public event Action? OnPreviewClicked;

        [Header("Containers")]
        [SerializeField] private GameObject emptyContainer;

        [SerializeField] private GameObject savingContainer;
        [SerializeField] private GameObject fullContainer;
        [SerializeField] private GameObject hoverEmptyContainer;
        [SerializeField] private GameObject loadingContainer;

        [Header("Buttons")]
        [SerializeField] private Button? saveButton;

        [SerializeField] private Button? equipButton;
        [SerializeField] private Button? unEquipButton;
        [SerializeField] private Button? deleteButton;
        [SerializeField] private Button? previewButton;

        [Header("Full State UI")]
        [SerializeField]
        private Image outfitThumbnail;

        [SerializeField]
        private Image outfitThumbnailEmpty;

        [Header("Placeholders & Empty State")]
        [SerializeField] private Image emptyStateSilhouette; 

        [SerializeField]
        private Image outfitEquippedOutline;
        
        [SerializeField]
        private Image outfitHoverOutline;

        [field: SerializeField]
        public HoverHandler hoverHandler { get; private set; }

        [field: SerializeField]
        private SkeletonLoadingView loadingView { get; set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig EquipWearableAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig SaveOutfitAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig DeleteOutfitAudio { get; private set; }

        private void Awake()
        {
            saveButton?.onClick.AddListener(() =>
            {
                OnSaveClicked?.Invoke();

                if (SaveOutfitAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(SaveOutfitAudio);
            });
            
            equipButton?.onClick.AddListener(() =>
            {
                OnEquipClicked?.Invoke();

                if (EquipWearableAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(EquipWearableAudio);
            });

            deleteButton?.onClick.AddListener(() =>
            {
                OnDeleteClicked?.Invoke();

                if (DeleteOutfitAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(DeleteOutfitAudio);
            });

            previewButton?.onClick.AddListener(() =>
            {
                OnPreviewClicked?.Invoke();
                if (ClickAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
            });

            if (outfitThumbnailEmpty != null && emptyStateSilhouette != null)
                outfitThumbnailEmpty.sprite = emptyStateSilhouette.sprite;
        }

        public void ShowEmptyState(bool isHovering)
        {
            emptyContainer.SetActive(!isHovering);
            hoverEmptyContainer.SetActive(isHovering);
            fullContainer.SetActive(false);
            savingContainer.SetActive(false);
            loadingView.HideLoading();
            loadingContainer.SetActive(false);
        }

        public void ShowLoadingState()
        {
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            fullContainer.SetActive(false);
            savingContainer.SetActive(false);
            loadingView.ShowLoading();
            loadingContainer.SetActive(true);
        }

        public void ShowStateSaving()
        {
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            loadingContainer.SetActive(false);
            fullContainer.SetActive(false);
            savingContainer.SetActive(true);
        }

        public void ShowFullState(Texture2D thumbnail, bool isHovered)
        {
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            savingContainer.SetActive(false);
            fullContainer.SetActive(true);
            loadingView.HideLoading();
            loadingContainer.SetActive(false);

            bool hasRealThumbnail = thumbnail != null;

            outfitThumbnail.gameObject.SetActive(hasRealThumbnail);
            outfitThumbnailEmpty.gameObject.SetActive(!hasRealThumbnail);
            if (hasRealThumbnail)
            {
                outfitThumbnail.sprite = Sprite.Create(thumbnail, new Rect(0, 0, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f));
                outfitThumbnail.color = new Color(1, 1, 1, 1);
            }

            outfitHoverOutline?.gameObject.SetActive(isHovered);
            unEquipButton?.gameObject.SetActive(false);
            
            if (isHovered)
            {
                deleteButton?.gameObject.SetActive(true);
                equipButton?.gameObject.SetActive(true);
            }
            else
            {
                deleteButton?.gameObject.SetActive(false);
                equipButton?.gameObject.SetActive(false);
            }
        }

        public void AnimateHover()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
            
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            outfitHoverOutline.transform.localScale = Vector3.zero;
            outfitHoverOutline.gameObject.SetActive(true);
            transform.DOScale(hoveredScale, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            outfitHoverOutline.transform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
        }

        public void AnimateExit()
        {
            cts?.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            transform.DOScale(Vector3.one, ANIMATION_TIME).SetEase(Ease.Flash).ToUniTask(cancellationToken: cts.Token);
            outfitHoverOutline.transform.DOScale(Vector3.zero, ANIMATION_TIME).SetEase(Ease.Flash)
                .OnComplete(() => outfitHoverOutline.gameObject.SetActive(false)).ToUniTask(cancellationToken: cts.Token);
        }

        private void OnDestroy()
        {
            saveButton?.onClick.RemoveAllListeners();
            equipButton?.onClick.RemoveAllListeners();
            deleteButton?.onClick.RemoveAllListeners();
            previewButton?.onClick.RemoveAllListeners();
        }

        public void SetEquipped(bool equipped)
        {
            outfitEquippedOutline.gameObject.SetActive(equipped);
        }
    }
}