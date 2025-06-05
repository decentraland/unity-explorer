using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        private const string WORLD_LINK_ID = "WORLD_LINK_ID";
        private const string CREATE_COMMUNITY_TITLE = "Create a Community";
        private const string EDIT_COMMUNITY_TITLE = "Edit Community";

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private UniTaskCompletionSource closeTaskCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.ConvertGetNameDescriptionUrlsToClickableLinks(GoToAnyLinkFromGetNameDescription);
            viewInstance.GetNameButtonClicked += GoToGetNameLink;
            viewInstance.CancelButtonClicked += OnCancelAction;
            viewInstance.SelectProfilePictureButtonClicked += OpenImageSelection;
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetAccess( /*inputData.CanCreateCommunities*/ true);
            viewInstance.SetCreationPanelTitle(string.IsNullOrEmpty(inputData.CommunityId) ? CREATE_COMMUNITY_TITLE : EDIT_COMMUNITY_TITLE);
        }

        protected override void OnViewShow() =>
            DisableShortcutsInput();

        protected override void OnViewClose() =>
            RestoreInput();

        public override void Dispose()
        {
            if (!viewInstance)
                return;

            viewInstance.GetNameButtonClicked -= GoToGetNameLink;
            viewInstance.CancelButtonClicked -= OnCancelAction;
            viewInstance.SelectProfilePictureButtonClicked -= OpenImageSelection;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.backgroundCloseButton.OnClickAsync(ct),
                closeTaskCompletionSource.Task);

        private void GoToAnyLinkFromGetNameDescription(string id)
        {
            if (id != WORLD_LINK_ID)
                return;

            webBrowser.OpenUrl($"{webBrowser.GetUrl(DecentralandUrl.DecentralandWorlds)}&utm_campaign=communities");
            viewInstance.PlayOnLinkClickAudio();
        }

        private void GoToGetNameLink() =>
            webBrowser.OpenUrl(DecentralandUrl.MarketplaceClaimName);

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void RestoreInput() =>
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);

        private void OnCancelAction() =>
            closeTaskCompletionSource.TrySetResult();

        private void OpenImageSelection()
        {

        }
    }
}
