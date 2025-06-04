using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using MVC;
using System.Threading;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        private const string WORLD_LINK_ID = "WORLD_LINK_ID";

        private readonly IWebBrowser webBrowser;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.ConvertGetNameDescriptionUrlsToClickableLinks(GoToAnyLinkFromGetNameDescription);
            viewInstance.GetNameButtonClicked += GoToGetNameLink;
        }

        public override void Dispose()
        {
            viewInstance.GetNameButtonClicked -= GoToGetNameLink;
            base.Dispose();
        }

        protected override void OnBeforeViewShow() =>
            viewInstance.SetAsClaimedName(inputData.HasClaimedName);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.backgroundCloseButton.OnClickAsync(ct), viewInstance!.cancelButton.OnClickAsync(ct));

        private void GoToAnyLinkFromGetNameDescription(string id)
        {
            if (id != WORLD_LINK_ID)
                return;

            webBrowser.OpenUrl(DecentralandUrl.DecentralandWorlds);
            viewInstance.PlayOnLinkClickAudio();
        }

        private void GoToGetNameLink() =>
            webBrowser.OpenUrl(DecentralandUrl.MarketplaceClaimName);
    }
}
