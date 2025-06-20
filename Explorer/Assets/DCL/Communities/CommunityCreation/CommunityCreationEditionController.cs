using Crosstales;
using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
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
        private const string GET_COMMUNITY_PLACES_ERROR_MESSAGE = "There was an error getting the community places. Please try again.";
        private const string INCOMPATIBLE_IMAGE_ERROR = "Invalid image file selected. Please check file type and size.";
        private const string FILE_BROWSER_TITLE = "Select image";
        private const int MAX_IMAGE_SIZE_BYTES = 512000; // 500 KB
        private const int MAX_IMAGE_DIMENSION_PIXELS = 512;
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly string[] allowedImageExtensions = { "jpg", "png" };

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource createCommunityCts;
        private CancellationTokenSource loadLandsAndWorldsCts;
        private CancellationTokenSource loadCommunityDataCts;
        private CancellationTokenSource showErrorCts;
        private CancellationTokenSource openImageSelectionCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private struct CommunityPlace
        {
            public string Id;
            public bool IsWorld;
            public string Name;
        }

        private readonly List<CommunityPlace> currentCommunityPlaces = new ();
        private readonly List<CommunityPlace> addedCommunityPlaces = new ();
        private byte[] lastSelectedImageData;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            ICommunitiesDataProvider dataProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;

            FileBrowser.Instance.AllowSyncCalls = true;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.ConfigureImageController(webRequestController);
            viewInstance.ConvertGetNameDescriptionUrlsToClickableLinks(GoToAnyLinkFromGetNameDescription);
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
            lastSelectedImageData = null;
            closeTaskCompletionSource = new UniTaskCompletionSource();
            viewInstance!.SetAccess(inputData.CanCreateCommunities);
            viewInstance.SetAsEditionMode(!string.IsNullOrEmpty(inputData.CommunityId));

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
            viewInstance.CleanCreationPanel();
            RestoreInput();

            createCommunityCts?.SafeCancelAndDispose();
            loadLandsAndWorldsCts?.SafeCancelAndDispose();
            loadCommunityDataCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openImageSelectionCts?.SafeCancelAndDispose();
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
            openImageSelectionCts?.SafeCancelAndDispose();
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
            openImageSelectionCts = openImageSelectionCts.SafeRestart();
            OpenImageSelectionAsync(openImageSelectionCts.Token).Forget();
        }

        private async UniTaskVoid OpenImageSelectionAsync(CancellationToken ct)
        {
            viewInstance!.backgroundCloseButton.enabled = false;

            FileBrowser.Instance.OpenSingleFile(FILE_BROWSER_TITLE, "", "", allowedImageExtensions);
            byte[] data = FileBrowser.Instance.CurrentOpenSingleFileData;

            // Due to a bug in the file browser (for Mac), we need to wait 2 frames after we close it to ensure we don't click accidentally in the background close button.
            // TODO: Investigate how to fix this properly.
            await UniTask.DelayFrame(2, cancellationToken: ct);
            viewInstance!.backgroundCloseButton.enabled = true;

            if (data != null)
            {
                Texture2D texture = data.CTToTexture();

                if (texture.width > MAX_IMAGE_DIMENSION_PIXELS || texture.height > MAX_IMAGE_DIMENSION_PIXELS || data.Length > MAX_IMAGE_SIZE_BYTES)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    viewInstance!.WarningNotificationView.AnimatedShowAsync(INCOMPATIBLE_IMAGE_ERROR, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token).Forget();
                    return;
                }

                viewInstance!.SetProfileSelectedImage(data.CTToSprite(texture));
                lastSelectedImageData = data;
            }
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
                PlacesData.IPlacesAPIResponse placesResponse = await placesAPIService.GetPlacesByOwnerAsync(ownProfile.UserId, ct);

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
                PlacesData.IPlacesAPIResponse worlds = await placesAPIService.GetWorldsByOwnerAsync(ownProfile.UserId, ct);

                foreach (PlacesData.PlaceInfo worldInfo in worlds.Data)
                {
                    placesToAdd.Add(worldInfo.world_name);
                    currentCommunityPlaces.Add(new CommunityPlace
                    {
                        Id = worldInfo.id,
                        IsWorld = true,
                        Name = worldInfo.world_name,
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

            AddPlaceTag(selectedPlace, isRemovalAllowed: true);
        }

        private void AddPlaceTag(CommunityPlace place, bool isRemovalAllowed, bool updateScrollPosition = true)
        {
            viewInstance!.AddPlaceTag(place.Id, place.IsWorld, place.Name, isRemovalAllowed, updateScrollPosition);
            addedCommunityPlaces.Add(place);
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

            // Load community data
            var getCommunityResult = await dataProvider.GetCommunityAsync(inputData.CommunityId, ct)
                                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!getCommunityResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                return;
            }

            viewInstance.SetProfileSelectedImage(imageUrl: getCommunityResult.Value.data.thumbnails?.raw);
            viewInstance.SetCommunityName(getCommunityResult.Value.data.name);
            viewInstance.SetCommunityDescription(getCommunityResult.Value.data.description);
            viewInstance.SetCreationPanelAsLoading(false);

            // Load community places ids
            var getCommunityPlacesResult = await dataProvider.GetCommunityPlacesAsync(inputData.CommunityId, ct)
                                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!getCommunityPlacesResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_PLACES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                return;
            }

            if (getCommunityPlacesResult.Value is { Count: > 0 })
            {
                // Load places details
                var getPlacesDetailsResult = await  placesAPIService.GetPlacesByIdsAsync(getCommunityPlacesResult.Value, ct)
                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!getPlacesDetailsResult.Success)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_PLACES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                    return;
                }

                if (getPlacesDetailsResult.Value is { data: { Count: > 0 } })
                {
                    foreach (PlacesData.PlaceInfo placeInfo in getPlacesDetailsResult.Value.data)
                    {
                        bool isOwner = getCommunityResult.Value.data.role == CommunityMemberRole.owner;
                        bool isRemovalAllowed = isOwner;

                        if (!isOwner)
                        {
                            foreach (CommunityPlace existingPlace in currentCommunityPlaces)
                            {
                                if (existingPlace.Id != placeInfo.id)
                                    continue;

                                isRemovalAllowed = true;
                                break;
                            }
                        }

                        AddPlaceTag(new CommunityPlace
                        {
                            Id = placeInfo.id,
                            IsWorld = !string.IsNullOrEmpty(placeInfo.world_name),
                            Name = $"{placeInfo.title} ({placeInfo.base_position})",
                        }, isRemovalAllowed, updateScrollPosition: false);
                    }
                }
            }
        }

        private void CreateCommunity(string name, string description, List<string> lands, List<string> worlds)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            CreateCommunityAsync(name, description, lands, worlds, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CreateCommunityAsync(string name, string description, List<string> lands, List<string> worlds, CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(null, name, description, lastSelectedImageData, lands, worlds, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(CREATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
        }

        private void UpdateCommunity(string name, string description, List<string> lands, List<string> worlds)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            UpdateCommunityAsync(inputData.CommunityId, name, description, lands, worlds, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid UpdateCommunityAsync(string id, string name, string description, List<string> lands, List<string> worlds, CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);
            var result = await dataProvider.CreateOrUpdateCommunityAsync(id, name, description, lastSelectedImageData, lands, worlds, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(UPDATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token);
                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();
        }
    }
}
