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
        public event Action? OnUnEquipClicked;
        public event Action? OnDeleteClicked;

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

        [Header("Full State UI")]
        [SerializeField]
        private Image outfitThumbnail;

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
            unEquipButton?.onClick.AddListener(() => OnUnEquipClicked?.Invoke());
            deleteButton?.onClick.AddListener(() => OnDeleteClicked?.Invoke());
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

        public void ShowFullState(Sprite thumbnail, bool isEquipped, bool isHovered)
        {
            emptyContainer.SetActive(false);
            hoverEmptyContainer.SetActive(false);
            savingContainer.SetActive(false);
            fullContainer.SetActive(true);
            loadingView.HideLoading();
            loadingContainer.SetActive(false);

            //outfitThumbnail.sprite = thumbnail;

            outfitHoverOutline?.gameObject.SetActive(isHovered);

            if (isHovered)
            {
                deleteButton?.gameObject.SetActive(true);
                equipButton?.gameObject.SetActive(!isEquipped);
                unEquipButton?.gameObject.SetActive(isEquipped);
            }
            else
            {
                deleteButton?.gameObject.SetActive(false);
                equipButton?.gameObject.SetActive(false);
                unEquipButton?.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Clean up listeners
            saveButton?.onClick.RemoveAllListeners();
            equipButton?.onClick.RemoveAllListeners();
            unEquipButton?.onClick.RemoveAllListeners();
            deleteButton?.onClick.RemoveAllListeners();
        }
    }
}