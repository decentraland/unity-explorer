using Crosstales;
using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        private const string WORLD_LINK_ID = "WORLD_LINK_ID";
        private const string CREATE_COMMUNITY_ERROR_MESSAGE = "There was an error creating community. Please try again.";
        private const string UPDATE_COMMUNITY_ERROR_MESSAGE = "There was an error updating community. Please try again.";
        private const string GET_COMMUNITY_ERROR_MESSAGE = "There was an error getting the community. Please try again.";
        private const string INCOMPATIBLE_IMAGE_ERROR = "Invalid image file selected. Please check file type and size.";
        private const string FILE_BROWSER_TITLE = "Select image";
        private const int MAX_IMAGE_SIZE_BYTES = 512000; // 500 KB
        private const int MAX_IMAGE_DIMENSION_PIXELS = 512;
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly CommunityCreationEditionEventBus communityCreationEditionEventBus;
        private readonly string[] allowedImageExtensions = { "jpg", "png" };

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource createCommunityCts;
        private CancellationTokenSource loadLandsAndWorldsCts;
        private CancellationTokenSource loadCommunityDataCts;
        private CancellationTokenSource showErrorCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private struct CommunityPlace
        {
            public string Id;
            public bool IsWorld;
            public string Name;
        }

        private readonly List<CommunityPlace> currentCommunityPlaces = new ();
        private readonly List<CommunityPlace> addedCommunityPlaces = new ();
        private byte[] currentThumbnail;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            ICommunitiesDataProvider dataProvider,
            INftNamesProvider nftNamesProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            CommunityCreationEditionEventBus communityCreationEditionEventBus) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
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
            viewInstance.SaveCommunityButtonClicked += UpdateCommunity;
            viewInstance.AddPlaceButtonClicked += AddCommunityPlace;
            viewInstance.RemovePlaceButtonClicked += RemoveCommunityPlace;
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetAccess(inputData.CanCreateCommunities);
            viewInstance.SetAsEditionMode(!string.IsNullOrEmpty(inputData.CommunityId));

            currentThumbnail = null;
            loadLandsAndWorldsCts = loadLandsAndWorldsCts.SafeRestart();
            LoadLandsAndWorldsAsync(loadLandsAndWorldsCts.Token).Forget();

            if (!string.IsNullOrEmpty(inputData.CommunityId))
            {
                // EDITION MODE
                loadCommunityDataCts = loadCommunityDataCts.SafeRestart();
                LoadCommunityDataAsync(loadCommunityDataCts.Token).Forget();
            }
        }

        protected override void OnViewShow() =>
            DisableShortcutsInput();

        protected override void OnViewClose()
        {
            RestoreInput();

            createCommunityCts?.SafeCancelAndDispose();
            loadLandsAndWorldsCts?.SafeCancelAndDispose();
            loadCommunityDataCts?.SafeCancelAndDispose();
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
            viewInstance.SaveCommunityButtonClicked -= UpdateCommunity;
            viewInstance.AddPlaceButtonClicked -= AddCommunityPlace;
            viewInstance.RemovePlaceButtonClicked -= RemoveCommunityPlace;

            createCommunityCts?.SafeCancelAndDispose();
            loadLandsAndWorldsCts?.SafeCancelAndDispose();
            loadCommunityDataCts?.SafeCancelAndDispose();
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
            FileBrowser.Instance.OpenSingleFile(FILE_BROWSER_TITLE, "", "", allowedImageExtensions);
            byte[] data = FileBrowser.Instance.CurrentOpenSingleFileData;
            Texture2D texture = data.CTToTexture();

            if (texture.width > MAX_IMAGE_DIMENSION_PIXELS || texture.height > MAX_IMAGE_DIMENSION_PIXELS || data.Length > MAX_IMAGE_SIZE_BYTES)
            {
                showErrorCts = showErrorCts.SafeRestart();
                viewInstance!.WarningNotificationView.AnimatedShowAsync(INCOMPATIBLE_IMAGE_ERROR, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token).Forget();
                return;
            }

            viewInstance!.SetProfileSelectedImage(data.CTToSprite());
            currentThumbnail = data;
        }

        private async UniTaskVoid LoadLandsAndWorldsAsync(CancellationToken ct)
        {
            viewInstance!.SetCreationPanelAsLoading(true);
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
                        IsWorld = false,
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
                        IsWorld = true,
                        Name = $"{name}.dcl.eth",
                    });
                }
            }

            viewInstance.SetPlacesSelector(placesToAdd);
            viewInstance!.SetCreationPanelAsLoading(false);
        }

        private void AddCommunityPlace(int index)
        {
            if (index >= currentCommunityPlaces.Count)
                return;

            CommunityPlace selectedPlace = currentCommunityPlaces[index];
            if (addedCommunityPlaces.Exists(place => place.Id == selectedPlace.Id))
                return;

            viewInstance!.AddPlaceTag(selectedPlace.Id, selectedPlace.IsWorld, selectedPlace.Name);
            addedCommunityPlaces.Add(selectedPlace);
        }

        private void RemoveCommunityPlace(int index)
        {
            if (index >= addedCommunityPlaces.Count)
                return;

            viewInstance!.RemovePlaceTag(addedCommunityPlaces[index].Id);
            addedCommunityPlaces.RemoveAt(index);
        }

        private async UniTaskVoid LoadCommunityDataAsync(CancellationToken ct)
        {
            viewInstance!.SetCreationPanelAsLoading(true);

            var result = await dataProvider.GetCommunityAsync(inputData.CommunityId, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                return;
            }

            viewInstance.SetCommunityName(result.Value.data.name);
            viewInstance.SetCommunityDescription(result.Value.data.description);
            viewInstance.SetCreationPanelAsLoading(false);
        }

        private void CreateCommunity(string name, string description, List<string> lands, List<string> worlds)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            CreateCommunityAsync(name, description, lands, worlds, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CreateCommunityAsync(string name, string description, List<string> lands, List<string> worlds, CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(null, name, description, currentThumbnail, lands, worlds, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(CREATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
            communityCreationEditionEventBus.OnCommunityCreated();
        }

        private void UpdateCommunity(string name, string description)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            UpdateCommunityAsync(inputData.CommunityId, name, description, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid UpdateCommunityAsync(string id, string name, string description, CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(id, name, description, null, null, null, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(UPDATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
            communityCreationEditionEventBus.OnCommunityCreated();
        }
    }
}
