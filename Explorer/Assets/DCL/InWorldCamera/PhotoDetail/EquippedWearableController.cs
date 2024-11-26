using Cysharp.Threading.Tasks;
using DCL.Browser;
using System;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableController : IDisposable
    {
        internal readonly EquippedWearableView view;
        private readonly IWebBrowser webBrowser;

		public EquippedWearableController(EquippedWearableView view,
            IWebBrowser webBrowser)
	    {
            this.view = view;
            this.webBrowser = webBrowser;
        }

        public async UniTask LoadWearable(string urn, CancellationToken ct)
        {
            view.wearableBuyButton.onClick.AddListener(BuyWearableButtonClicked);
        }

        private void BuyWearableButtonClicked()
        {
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await UniTask.Delay((int)(view.buyButtonAnimationDuration * 1000));
                //TODO: get the marketplace URL from the wearable
                webBrowser.OpenUrl("https://market.decentraland.org/browse?category=wearables");
            }

            AnimateAndAwaitAsync().Forget();
        }

        public void Release()
        {
            view.wearableBuyButton.onClick.RemoveListener(BuyWearableButtonClicked);
        }

        public void Dispose()
        {
            view.wearableBuyButton.onClick.RemoveAllListeners();
        }
    }
}
