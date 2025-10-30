using System;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.AvatarSection.Outfits.Banner
{
    public class OutfitBannerView : MonoBehaviour
    {
        public event Action OnGetANameClicked;
        public event Action<string> OnWorldLinkClicked;

        [SerializeField] private Button getANameButton;
        [SerializeField] private TMP_Text_ClickeableLink worldLink;

        private void OnEnable()
        {
            getANameButton.onClick.AddListener(() => OnGetANameClicked?.Invoke());
            worldLink.OnLinkClicked += OnWorldLinkClicked;
        }

        private void OnDisable()
        {
            getANameButton.onClick.RemoveAllListeners();
            worldLink.OnLinkClicked -= OnWorldLinkClicked;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}