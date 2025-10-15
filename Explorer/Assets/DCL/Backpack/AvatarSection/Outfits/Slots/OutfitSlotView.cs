using System;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.AvatarSection.Outfits.Slots
{
    public class OutfitSlotView : MonoBehaviour
    {
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

        private void Awake()
        {
            saveButton?.onClick.AddListener(() => OnSaveClicked?.Invoke());
            equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            deleteButton?.onClick.AddListener(() => OnDeleteClicked?.Invoke());
            previewButton?.onClick.AddListener(() => OnPreviewClicked?.Invoke());

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
                // We have a real thumbnail, so configure the 'outfitThumbnail' Image.
                // Using .sprite is fine here since this Image component will only ever show generated sprites.
                outfitThumbnail.sprite = Sprite.Create(thumbnail, new Rect(0, 0, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f));
                outfitThumbnail.color = Color.white;
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