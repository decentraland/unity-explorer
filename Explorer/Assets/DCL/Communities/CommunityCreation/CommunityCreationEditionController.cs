using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System.Collections.Generic;
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
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly CommunityCreationEditionEventBus communityCreationEditionEventBus;

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource createCommunityCts;
        private CancellationTokenSource loadLandsAndWorldsCts;
        private CancellationTokenSource showErrorCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private struct CommunityPlace
        {
            public string Id;
            public string Name;
        }

        private readonly List<CommunityPlace> currentCommunityPlaces = new ();
        private readonly List<CommunityPlace> addedCommunityPlaces = new ();

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            ICommunitiesDataProvider dataProvider,
            WarningNotificationView warningNotificationView,
            INftNamesProvider nftNamesProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            CommunityCreationEditionEventBus communityCreationEditionEventBus) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
            this.warningNotificationView = warningNotificationView;
            this.nftNamesProvider = nftNamesProvider;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.communityCreationEditionEventBus = communityCreationEditionEventBus;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.ConvertGetNameDescriptionUrlsToClickableLinks(GoToAnyLinkFromGetNameDescription);
            viewInstance.GetNameButtonClicked += GoToGetNameLink;
            viewInstance.CancelButtonClicked += OnCancelAction;
            viewInstance.SelectProfilePictureButtonClicked += OpenImageSelection;
            viewInstance.CreateCommunityButtonClicked += CreateCommunity;
            viewInstance.AddPlaceButtonClicked += AddCommunityPlace;
            viewInstance.RemovePlaceButtonClicked += RemoveCommunityPlace;
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetAccess(inputData.CanCreateCommunities);
            viewInstance.SetCreationPanelTitle(string.IsNullOrEmpty(inputData.CommunityId) ? CREATE_COMMUNITY_TITLE : EDIT_COMMUNITY_TITLE);

            loadLandsAndWorldsCts = loadLandsAndWorldsCts.SafeRestart();
            LoadLandsAndWorldsAsync(loadLandsAndWorldsCts.Token).Forget();
        }

        protected override void OnViewShow() =>
            DisableShortcutsInput();

        protected override void OnViewClose()
        {
            RestoreInput();

            createCommunityCts?.SafeCancelAndDispose();
            loadLandsAndWorldsCts?.SafeCancelAndDispose();
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
            viewInstance.AddPlaceButtonClicked -= AddCommunityPlace;
            viewInstance.RemovePlaceButtonClicked -= RemoveCommunityPlace;

            createCommunityCts?.SafeCancelAndDispose();
            loadLandsAndWorldsCts?.SafeCancelAndDispose();
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
            viewInstance!.SetCreationPanelAsLoading(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(null, name, description, null, null, null, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await warningNotificationView.AnimatedShowAsync(CREATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCreationPanelAsLoading(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
            communityCreationEditionEventBus.OnCommunityCreated();
        }

        private async UniTaskVoid LoadLandsAndWorldsAsync(CancellationToken ct)
        {
            currentCommunityPlaces.Clear();
            addedCommunityPlaces.Clear();
            List<string> placesToAdd = new();

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile != null)
            {
                // Lands owned or managed by the user
                PlacesData.IPlacesAPIResponse placesResponse = await placesAPIService.SearchPlacesAsync(0, 1000, ct, "santi");

                foreach (PlacesData.PlaceInfo placeInfo in placesResponse.Data)
                {
                    var placeText = $"{placeInfo.title} ({placeInfo.base_position})";
                    placesToAdd.Add(placeText);
                    currentCommunityPlaces.Add(new CommunityPlace
                    {
                        Id = placeInfo.id,
                        Name = placeText,
                    });
                }

                // Worlds
                INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(ownProfile.UserId), 1, 1000, ct);

                foreach (string name in names.Names)
                {
                    placesToAdd.Add($"{name}.dcl.eth");
                    currentCommunityPlaces.Add(new CommunityPlace
                    {
                        Id = $"{name}.dcl.eth",
                        Name = $"{name}.dcl.eth",
                    });
                }
            }

            viewInstance.SetPlacesSelector(placesToAdd);
        }

        private void AddCommunityPlace(int index)
        {
            if (index >= currentCommunityPlaces.Count)
                return;

            CommunityPlace selectedPlace = currentCommunityPlaces[index];
            if (addedCommunityPlaces.Exists(place => place.Id == selectedPlace.Id))
                return;

            viewInstance!.AddPlaceTag(selectedPlace.Id, selectedPlace.Name);
            addedCommunityPlaces.Add(selectedPlace);
        }

        private void RemoveCommunityPlace(int index)
        {
            if (index >= addedCommunityPlaces.Count)
                return;

            viewInstance!.RemovePlaceTag(addedCommunityPlaces[index].Id);
            addedCommunityPlaces.RemoveAt(index);
        }
    }
}
