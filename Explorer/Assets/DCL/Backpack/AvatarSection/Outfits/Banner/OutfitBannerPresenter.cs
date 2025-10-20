using System;
using System.Threading.Tasks;

namespace DCL.Backpack.AvatarSection.Outfits.Banner
{
    public class OutfitBannerPresenter : IDisposable
    {
        private readonly OutfitBannerView view;
        private readonly Action onGetANameClicked;
        private readonly Action<string> onWorldLinkClicked;

        public OutfitBannerPresenter(OutfitBannerView view,
            Action onGetANameClicked,
            Action<string> onWorldLinkClicked)
        {
            this.view = view;
            this.onGetANameClicked = onGetANameClicked;
            this.onWorldLinkClicked = onWorldLinkClicked;

            view.OnWorldLinkClicked += OnWorldLinkClicked;
            view.OnGetANameClicked += OnGetANameClicked;
        }

        private void OnWorldLinkClicked(string url)
        {
            onWorldLinkClicked?.Invoke(url);
        }

        private void OnGetANameClicked()
        {
            onGetANameClicked?.Invoke();
        }

        public void Dispose()
        {
            view.OnGetANameClicked -= OnGetANameClicked;
            view.OnWorldLinkClicked -= OnWorldLinkClicked;
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }
    }
}