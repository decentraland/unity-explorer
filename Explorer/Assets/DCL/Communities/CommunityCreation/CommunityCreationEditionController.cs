using CommunicationData.URLHelpers;
using Crosstales;
using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Communities.CommunitiesCard;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities.Extensions;
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
        private const string GET_PLACES_ERROR_MESSAGE = "There was an error getting places. Please try again.";
        private const string GET_WORLDS_ERROR_MESSAGE = "There was an error getting worlds. Please try again.";
        private const string GET_COMMUNITY_ERROR_MESSAGE = "There was an error getting the community. Please try again.";
        private const string GET_COMMUNITY_PLACES_ERROR_MESSAGE = "There was an error getting the community places. Please try again.";
        private const string INCOMPATIBLE_IMAGE_GENERAL_ERROR = "Invalid image file selected. Please check file type and size.";
        private const string INCOMPATIBLE_IMAGE_WEIGHT_ERROR = "Selected image exceeds 500KB size limit.";
        private const string INCOMPATIBLE_IMAGE_RESOLUTION_ERROR = "Selected image exceeds 512x512 px size limit.";
        private const string FILE_BROWSER_TITLE = "Select image";
        private const int MAX_IMAGE_SIZE_BYTES = 512000; // 500 KB
        private const int MAX_IMAGE_DIMENSION_PIXELS = 512;
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly string[] allowedImageExtensions = { "jpg", "png" };

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource? createCommunityCts;
        private CancellationTokenSource? loadPanelCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openImageSelectionCts;
        private CancellationTokenSource? openCommunityCardAfterCreationCts;

        private Sprite? lastSelectedProfileThumbnail;
        private bool isProfileThumbnailDirty;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private struct CommunityPlace
        {
            public string Id;
            public bool IsWorld;
            public string Name;
        }

        private readonly List<CommunityPlace> currentCommunityPlaces = new ();
        private readonly List<CommunityPlace> addedCommunityPlaces = new ();
        private byte[]? lastSelectedImageData;
        private ThumbnailLoader? thumbnailLoader;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            CommunitiesDataProvider dataProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IMVCManager mvcManager) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;

            FileBrowser.Instance.AllowSyncCalls = true;
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
            lastSelectedImageData = null;
            closeTaskCompletionSource = new UniTaskCompletionSource();
            thumbnailLoader = new ThumbnailLoader(inputData.ThumbnailSpriteCache);
            viewInstance!.SetAccess(inputData.CanCreateCommunities);
            viewInstance.SetAsEditionMode(!string.IsNullOrEmpty(inputData.CommunityId));

            loadPanelCts = loadPanelCts.SafeRestart();
            LoadPanelAsync(loadPanelCts.Token).Forget();
        }

        protected override void OnViewShow() =>
            DisableShortcutsInput();

        protected override void OnViewClose()
        {
            viewInstance!.CleanCreationPanel();
            RestoreInput();

            createCommunityCts?.SafeCancelAndDispose();
            loadPanelCts?.SafeCancelAndDispose();
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
            loadPanelCts?.SafeCancelAndDispose();
            showErrorCts?.SafeCancelAndDispose();
            openImageSelectionCts?.SafeCancelAndDispose();
            openCommunityCardAfterCreationCts?.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance!.backgroundCloseButton.OnClickAsync(ct),
                closeTaskCompletionSource.Task);

        private async UniTask LoadPanelAsync(CancellationToken ct)
        {
            viewInstance!.SetCreationPanelAsLoading(true);
            await LoadLandsAndWorldsAsync(ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!string.IsNullOrEmpty(inputData.CommunityId))
            {
                // EDITION MODE
                await LoadCommunityDataAsync(ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);
            }

            viewInstance!.SetCreationPanelAsLoading(false);
        }

        private void GoToAnyLinkFromGetNameDescription(string id)
        {
            if (id != WORLD_LINK_ID)
                return;

            webBrowser.OpenUrl(webBrowser.GetUrl(DecentralandUrl.DecentralandWorlds).Append("&utm_campaign=communities"));
            viewInstance!.PlayOnLinkClickAudio();
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

                bool isInvalidImageByWeight = data.Length > MAX_IMAGE_SIZE_BYTES;
                bool isInvalidImageByResolution = texture.width > MAX_IMAGE_DIMENSION_PIXELS || texture.height > MAX_IMAGE_DIMENSION_PIXELS;
                if (isInvalidImageByWeight || isInvalidImageByResolution)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    viewInstance!.WarningNotificationView.AnimatedShowAsync(
                        isInvalidImageByWeight && isInvalidImageByResolution ?
                            INCOMPATIBLE_IMAGE_GENERAL_ERROR :
                            isInvalidImageByWeight ? INCOMPATIBLE_IMAGE_WEIGHT_ERROR : INCOMPATIBLE_IMAGE_RESOLUTION_ERROR,
                        WARNING_MESSAGE_DELAY_MS,
                        showErrorCts.Token)
                                 .SuppressToResultAsync(ReportCategory.COMMUNITIES)
                                 .Forget();
                    return;
                }

                lastSelectedProfileThumbnail = data.CTToSprite(texture);
                isProfileThumbnailDirty = true;

                viewInstance!.SetProfileSelectedImage(lastSelectedProfileThumbnail);

                lastSelectedImageData = data;
            }
        }

        private async UniTask LoadLandsAndWorldsAsync(CancellationToken ct)
        {
            currentCommunityPlaces.Clear();
            addedCommunityPlaces.Clear();
            List<string> placesToAdd = new ();

            var ownProfile = await selfProfile.ProfileAsync(ct);

            if (ownProfile != null)
            {
                // Lands owned or managed by the user
                var placesResult = await placesAPIService.GetPlacesByOwnerAsync(ownProfile.UserId, ct)
                                                         .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!placesResult.Success)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await viewInstance!.WarningNotificationView.AnimatedShowAsync(GET_PLACES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                foreach (PlacesData.PlaceInfo placeInfo in placesResult.Value.data)
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
                var worldsResult = await placesAPIService.GetWorldsByOwnerAsync(ownProfile.UserId, ct)
                                                         .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!worldsResult.Success)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await viewInstance!.WarningNotificationView.AnimatedShowAsync(GET_WORLDS_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                foreach (PlacesData.PlaceInfo worldInfo in worldsResult.Value.data)
                {
                    var worldText = $"{worldInfo.title} ({worldInfo.world_name})";
                    placesToAdd.Add(worldText);

                    currentCommunityPlaces.Add(new CommunityPlace
                    {
                        Id = worldInfo.id,
                        IsWorld = true,
                        Name = worldText,
                    });
                }
            }

            viewInstance!.SetPlacesSelector(placesToAdd);
        }

        private void AddCommunityPlace(int index)
        {
            if (index >= currentCommunityPlaces.Count)
                return;

            CommunityPlace selectedPlace = currentCommunityPlaces[index];

            foreach (var place in addedCommunityPlaces)
            {
                if (place.Id == selectedPlace.Id)
                    return;
            }

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

            viewInstance!.RemovePlaceTag(addedCommunityPlaces[index].Id, addedCommunityPlaces[index].Name);
            addedCommunityPlaces.RemoveAt(index);
        }

        private async UniTask LoadCommunityDataAsync(CancellationToken ct)
        {
            // Load community data
            var getCommunityResult = await dataProvider.GetCommunityAsync(inputData.CommunityId, ct)
                                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!getCommunityResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance!.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return;
            }

            viewInstance!.SetProfileSelectedImage(imageUrl: getCommunityResult.Value.data.thumbnails?.rawUri, thumbnailLoader);
            viewInstance.SetCommunityName(getCommunityResult.Value.data.name, getCommunityResult.Value.data.role == CommunityMemberRole.owner);
            viewInstance.SetCommunityDescription(getCommunityResult.Value.data.description);

            // Load community places ids
            var getCommunityPlacesResult = await dataProvider.GetCommunityPlacesAsync(inputData.CommunityId, ct)
                                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!getCommunityPlacesResult.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_PLACES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                  .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                return;
            }

            if (getCommunityPlacesResult.Value is { Count: > 0 })
            {
                // Load places details
                var getPlacesDetailsResult = await  placesAPIService.GetPlacesByIdsAsync(getCommunityPlacesResult.Value, ct)
                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!getPlacesDetailsResult.Success)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await viewInstance.WarningNotificationView.AnimatedShowAsync(GET_COMMUNITY_PLACES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                      .SuppressToResultAsync(ReportCategory.COMMUNITIES);
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
                            Name = string.IsNullOrEmpty(placeInfo.world_name) ?
                                $"{placeInfo.title} ({placeInfo.base_position})" :
                                $"{placeInfo.title} ({placeInfo.world_name})",
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

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();
                await viewInstance.WarningNotificationView.AnimatedShowAsync(CREATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                  .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            closeTaskCompletionSource.TrySetResult();

            openCommunityCardAfterCreationCts = openCommunityCardAfterCreationCts.SafeRestart();
            mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(result.Value.data.id, thumbnailLoader!.Cache)), openCommunityCardAfterCreationCts.Token).Forget();
        }

        private void UpdateCommunity(string name, string description, List<string> lands, List<string> worlds)
        {
            createCommunityCts = createCommunityCts.SafeRestart();
            UpdateCommunityAsync(inputData.CommunityId, name, description, lands, worlds, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid UpdateCommunityAsync(string id, string name, string description, List<string> lands, List<string> worlds,
            CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);

            var result = await dataProvider.CreateOrUpdateCommunityAsync(id, name, description, lastSelectedImageData, lands, worlds, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                showErrorCts = showErrorCts.SafeRestart();

                await viewInstance.WarningNotificationView.AnimatedShowAsync(UPDATE_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                  .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                viewInstance.SetCommunityCreationInProgress(false);
                return;
            }

            if (isProfileThumbnailDirty && lastSelectedProfileThumbnail != null)
            {
                thumbnailLoader!.Cache?.AddOrReplaceCachedSprite(result.Value.data.thumbnails?.rawUri, lastSelectedProfileThumbnail);
                isProfileThumbnailDirty = false;
                lastSelectedProfileThumbnail = null;
            }

            closeTaskCompletionSource.TrySetResult();
        }
    }
}
