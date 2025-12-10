using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Views
{
    public class GiftOpenedItemView : MonoBehaviour
    {
        [Header("State Containers")]
        [SerializeField] private GameObject loadingStateContainer;
        [SerializeField] private GameObject loadedStateContainer;
        [SerializeField] private SkeletonLoadingView loadingView;

        [Header("UI Elements")]
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }

        [SerializeField] public Image RarityBackground;
        [SerializeField] public Image FlapBackground;
        [SerializeField] public Image CategoryImage;

        private void Awake()
        {
            SetLoading();
        }

        public void SetLoading()
        {
            loadingStateContainer.SetActive(true);
            loadedStateContainer.SetActive(false);
            loadingView.ShowLoading();
            
            if (ThumbnailImageView != null) ThumbnailImageView.gameObject.SetActive(false);
        }

        /// <summary>
        ///     Sets the static styling (Rarity color, Icon) immediately.
        ///     This does NOT stop the loading animation.
        /// </summary>
        public void ConfigureAttributes(Sprite? rarityBg, Color flapColor, Sprite? categoryIcon)
        {
            if (RarityBackground != null) RarityBackground.sprite = rarityBg;
            if (FlapBackground != null) FlapBackground.color = flapColor;
            if (CategoryImage != null)
            {
                CategoryImage.sprite = categoryIcon;
                CategoryImage.gameObject.SetActive(categoryIcon != null);
            }
        }

        /// <summary>
        ///     Sets the image and finishes the loading state.
        /// </summary>
        public void SetLoadedState()
        {
            loadingView.HideLoading();
            loadingStateContainer.SetActive(false);
            loadedStateContainer.SetActive(true);
            
            if (ThumbnailImageView != null) ThumbnailImageView.gameObject.SetActive(true);
        }
    }
}