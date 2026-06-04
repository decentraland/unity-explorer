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
        [SerializeField] private GameObject? equipButtonContent;
        [SerializeField] private GameObject? equipSpinner;
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

        private bool isEquipLoading;

        private void Awake()
        {
            saveButton?.onClick.AddListener(() => OnSaveClicked?.Invoke());

            equipButton?.onClick.AddListener(() =>
            {
                if (isEquipLoading) return;

                OnEquipClicked?.Invoke();

                if (EquipWearableAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(EquipWearableAudio);
            });

            deleteButton?.onClick.AddListener(() => OnDeleteClicked?.Invoke());

            previewButton?.onClick.AddListener(() =>
            {
                if (isEquipLoading) return;

                OnPreviewClicked?.Invoke();
                if (ClickAudio != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);
            });

            if (outfitThumbnailEmpty != null && emptyStateSilhouette != null)
                outfitThumbnailEmpty.sprite = emptyStateSilhouette.sprite;
        }

        public void ShowEmptyState(bool isHovering)
        {
            ResetEquipButtonContent();
            emptyContainer.SetActive(!isHovering);
            hoverEmptyContainer.SetActive(isHovering);
            fullContainer.SetActive(false);
            savingContainer.SetActive(false);
            loadingView.HideLoading();
            loadingContainer.SetActive(false);
        }

        public void ShowLoadingState()
        {
            ResetEquipButtonContent();
            HideActionButtons();
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            fullContainer.SetActive(false);
            savingContainer.SetActive(false);
            loadingView.ShowLoading();
            loadingContainer.SetActive(true);
        }

        public void ShowStateSaving()
        {
            ResetEquipButtonContent();
            HideActionButtons();
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            loadingContainer.SetActive(false);
            fullContainer.SetActive(false);
            savingContainer.SetActive(true);
        }

        /// <summary>
        ///     Forces the per-slot action buttons hidden regardless of where they sit in the
        ///     prefab hierarchy. Some prefabs place equip/delete outside <see cref="fullContainer"/>,
        ///     so toggling the container alone isn't enough — these buttons would otherwise
        ///     remain visible from their previous hovered state during Save/Loading transitions.
        /// </summary>
        private void HideActionButtons()
        {
            deleteButton?.gameObject.SetActive(false);
            equipButton?.gameObject.SetActive(false);
            unEquipButton?.gameObject.SetActive(false);
        }

        public void ShowFullState(Texture2D? thumbnail, bool isHovered, bool isOperationBusy)
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
            if (thumbnail != null)
            {
                outfitThumbnail.sprite = Sprite.Create(thumbnail, new Rect(0, 0, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f));
                outfitThumbnail.color = new Color(1, 1, 1, 1);
            }

            outfitHoverOutline?.gameObject.SetActive(isHovered);
            unEquipButton?.gameObject.SetActive(false);

            // Equip stays available on hover even during a save/delete: it's safe because
            // SaveOutfitCommand snapshots equippedWearables before its await, and delete
            // doesn't touch equipped state at all. Delete is hidden during operations to
            // keep destructive actions serialized.
            equipButton?.gameObject.SetActive(isHovered);
            deleteButton?.gameObject.SetActive(isHovered && !isOperationBusy);
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

        public void SetEquipLoading(bool loading)
        {
            isEquipLoading = loading;
            equipButtonContent?.SetActive(!loading);
            equipSpinner?.SetActive(loading);
        }

        public void SetHoverEnabled(bool isEnabled)
        {
            if (hoverHandler == null) return;

            // Snap back if we're disabling while hovered — disabled HoverHandler won't fire OnPointerExit.
            if (!isEnabled && hoverHandler.enabled)
                AnimateExit();

            hoverHandler.enabled = isEnabled;
        }

        public void SetSaveInteractable(bool interactable)
        {
            if (saveButton != null) saveButton.interactable = interactable;
        }

        public void SetDeleteInteractable(bool interactable)
        {
            if (deleteButton != null) deleteButton.interactable = interactable;
        }

        public void ResetHoverState()
        {
            cts?.SafeCancelAndDispose();
            cts = null;

            transform.localScale = Vector3.one;

            if (outfitHoverOutline != null)
            {
                outfitHoverOutline.transform.localScale = Vector3.zero;
                outfitHoverOutline.gameObject.SetActive(false);
            }
        }

        private void ResetEquipButtonContent()
        {
            isEquipLoading = false;
            equipButtonContent?.SetActive(true);
            equipSpinner?.SetActive(false);
        }

        public void PlayDeleteOutfitSound()
        {
            if (DeleteOutfitAudio != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(DeleteOutfitAudio);
        }

        public void PlaySaveOutfitSound()
        {
            if (SaveOutfitAudio != null)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(SaveOutfitAudio);
        }
    }
}
