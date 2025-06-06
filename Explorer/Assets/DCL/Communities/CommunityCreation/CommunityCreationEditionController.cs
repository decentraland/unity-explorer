using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using DCL.Utilities.Extensions;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        private const string WORLD_LINK_ID = "WORLD_LINK_ID";
        private const string CREATE_COMMUNITY_TITLE = "Create a Community";
        private const string EDIT_COMMUNITY_TITLE = "Edit Community";
        private const string CREATE_COMMUNITY_ERROR_MESSAGE = "There was an error creating community. Please try again.";
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly WarningNotificationView warningNotificationView;

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource createCommunityCts;
        private CancellationTokenSource showErrorCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            ICommunitiesDataProvider dataProvider,
            WarningNotificationView warningNotificationView) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
            this.warningNotificationView = warningNotificationView;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.ConvertGetNameDescriptionUrlsToClickableLinks(GoToAnyLinkFromGetNameDescription);
            viewInstance.GetNameButtonClicked += GoToGetNameLink;
            viewInstance.CancelButtonClicked += OnCancelAction;
            viewInstance.SelectProfilePictureButtonClicked += OpenImageSelection;
            viewInstance.CreateCommunityButtonClicked += CreateCommunity;
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetAccess(inputData.CanCreateCommunities);
            viewInstance.SetCreationPanelTitle(string.IsNullOrEmpty(inputData.CommunityId) ? CREATE_COMMUNITY_TITLE : EDIT_COMMUNITY_TITLE);
        }

        protected override void OnViewShow() =>
            DisableShortcutsInput();

        protected override void OnViewClose()
        {
            RestoreInput();

            createCommunityCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
        }

        public override void Dispose()
        {
            if (!viewInstance)
                return;

            viewInstance.GetNameButtonClicked -= GoToGetNameLink;
            viewInstance.CancelButtonClicked -= OnCancelAction;
            viewInstance.SelectProfilePictureButtonClicked -= OpenImageSelection;
            viewInstance.CreateCommunityButtonClicked -= CreateCommunity;

            createCommunityCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
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

        private void CreateCommunity(string name, string description)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            CreateCommunityAsync(name, description, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CreateCommunityAsync(string name, string description, CancellationToken ct)
        {
            viewInstance!.SetCreationCommunityAsLoading(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(null, name, description, null, null, null, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(CREATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCreationCommunityAsLoading(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
        }
    }
}
