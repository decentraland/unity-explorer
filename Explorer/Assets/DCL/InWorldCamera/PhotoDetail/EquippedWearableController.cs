using Cysharp.Threading.Tasks;
using DCL.Browser;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableController
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

        }

        public void Release()
        {
            view.wearableBuyButton.onClick.RemoveListener(BuyWearableButtonClicked);
        }
    }
}
