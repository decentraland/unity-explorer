using Crosstales;
using Crosstales.FB;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionController : ControllerBase<CommunityCreationEditionView, CommunityCreationEditionParameter>
    {
        private const string WORLD_LINK_ID = "WORLD_LINK_ID";
        private const string CREATE_COMMUNITY_ERROR_MESSAGE = "There was an error creating community. Please try again.";
        private const string UPDATE_COMMUNITY_ERROR_MESSAGE = "There was an error updating community. Please try again.";
        private const string GET_PLACES_ERROR_MESSAGE = "There was an error getting places. Please try again.";
        private const string GET_WORLDS_ERROR_MESSAGE = "There was an error getting worlds. Please try again.";
        private const string GET_OWNERS_NAMES_ERROR_MESSAGE = "There was an error getting owners names. Please try again.";
        private const string GET_COMMUNITY_ERROR_MESSAGE = "There was an error getting the community. Please try again.";
        private const string GET_COMMUNITY_PLACES_ERROR_MESSAGE = "There was an error getting the community places. Please try again.";
        private const string INCOMPATIBLE_IMAGE_GENERAL_ERROR = "Invalid image file selected. Please check file type and size.";
        private const string INCOMPATIBLE_IMAGE_WEIGHT_ERROR = "Selected image exceeds 500KB size limit.";
        private const string INCOMPATIBLE_IMAGE_RESOLUTION_ERROR = "Selected image exceeds 512x512 px size limit.";
        private const string FILE_BROWSER_TITLE = "Select image";
        private const int MAX_IMAGE_SIZE_BYTES = 512000; // 500 KB
        private const int MAX_IMAGE_DIMENSION_PIXELS = 512;
        private const int WARNING_MESSAGE_DELAY_MS = 3000;
        private const string CONTENT_POLICY_LINK_ID = "CONTENT_POLICY_LINK_ID";
        private const string CODE_AND_ETHICS_LINK_ID = "CODE_AND_ETHICS_LINK_ID";

        private readonly IWebBrowser webBrowser;
        private readonly IInputBlock inputBlock;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ISelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly LambdasProfilesProvider lambdasProfilesProvider;
        private readonly string[] allowedImageExtensions = { "jpg", "png" };

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource? createCommunityCts;
        private CancellationTokenSource? loadPanelCts;
        private CancellationTokenSource? showErrorCts;
        private CancellationTokenSource? openImageSelectionCts;
        private CancellationTokenSource? openCommunityCardAfterCreationCts;

        private Sprite? lastSelectedProfileThumbnail;
        private bool isProfileThumbnailDirty;
        private string originalCommunityNameForEdition;
        private string originalCommunityDescriptionForEdition;
        private CommunityPrivacy? originalCommunityPrivacyForEdition;
        private readonly List<string> originalCommunityLandsForEdition = new ();
        private readonly List<string> originalCommunityWorldsForEdition = new ();

        private static readonly ListObjectPool<string> USER_IDS_POOL = new (defaultCapacity: 2);

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private class CommunityPlace
        {
            public readonly string Id;
            public readonly bool IsWorld;
            public readonly string Name;
            public readonly string OwnerId;
            public string OwnerName;

            public CommunityPlace(string id, bool isWorld, string name, string ownerId, string ownerName)
            {
                Id = id;
                IsWorld = isWorld;
                Name = name;
                OwnerId = ownerId;
                OwnerName = ownerName;
            }
        }

        private readonly List<CommunityPlace> currentCommunityPlaces = new ();
        private readonly List<CommunityPlace> addedCommunityPlaces = new ();
        private byte[]? lastSelectedImageData;
        private ThumbnailLoader? thumbnailLoader;

        public CommunityCreationEditionController(
            ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IInputBlock inputBlock,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            IPlacesAPIService placesAPIService,
            ISelfProfile selfProfile,
            IMVCManager mvcManager,
            LambdasProfilesProvider lambdasProfilesProvider) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.inputBlock = inputBlock;
            this.dataProvider = dataProvider;
            this.placesAPIService = placesAPIService;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.lambdasProfilesProvider = lambdasProfilesProvider;

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
            viewInstance.ContentPolicyAndCodeOfEthicsLinksClicked += OpenContentPolicyAndCodeOfEthicsLink;
            viewInstance.GoBackToCreationEditionButtonClicked += GoBackToCreationEditionModal;
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
            viewInstance.ContentPolicyAndCodeOfEthicsLinksClicked -= OpenContentPolicyAndCodeOfEthicsLink;
            viewInstance.GoBackToCreationEditionButtonClicked -= GoBackToCreationEditionModal;

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

            webBrowser.OpenUrl($"{webBrowser.GetUrl(DecentralandUrl.DecentralandWorlds)}&utm_campaign=communities");
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

                    currentCommunityPlaces.Add(
                        new CommunityPlace(
                            placeInfo.id,
                            false,
                            placeText,
                            placeInfo.owner,
                            string.Empty));
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

                    currentCommunityPlaces.Add(new CommunityPlace (
                        worldInfo.id,
                        true,
                        worldText,
                        worldInfo.owner,
                        string.Empty));
                }

                // Owners' names
                using PoolExtensions.Scope<List<string>> userIds = USER_IDS_POOL.AutoScope();
                foreach (var communityPlace in currentCommunityPlaces)
                {
                    if (userIds.Value.Contains(communityPlace.OwnerId))
                        continue;

                    userIds.Value.Add(communityPlace.OwnerId);
                }

                var getAvatarsDetailsResult = await lambdasProfilesProvider.GetAvatarsDetailsAsync(userIds.Value, ct)
                                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (getAvatarsDetailsResult.Success)
                {
                    foreach (var communityPlace in currentCommunityPlaces)
                    {
                        foreach (var avatarDetails in getAvatarsDetailsResult.Value)
                        {
                            if (avatarDetails.avatars.Count == 0 || !string.Equals(communityPlace.OwnerId, avatarDetails.avatars[0].userId, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            communityPlace.OwnerName = avatarDetails.avatars[0].name;
                            break;
                        }
                    }
                }
                else
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await viewInstance!.WarningNotificationView.AnimatedShowAsync(GET_OWNERS_NAMES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                       .SuppressToResultAsync(ReportCategory.COMMUNITIES);
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
            viewInstance!.AddPlaceTag(place.Id, place.IsWorld, place.Name, place.OwnerName, isRemovalAllowed, updateScrollPosition);
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

            originalCommunityNameForEdition = getCommunityResult.Value.data.name;
            originalCommunityDescriptionForEdition = getCommunityResult.Value.data.description;
            originalCommunityPrivacyForEdition = getCommunityResult.Value.data.privacy;
            originalCommunityLandsForEdition.Clear();
            originalCommunityWorldsForEdition.Clear();

            viewInstance!.SetProfileSelectedImage(imageUrl: getCommunityResult.Value.data.thumbnails?.raw, thumbnailLoader);
            viewInstance.SetCommunityName(getCommunityResult.Value.data.name, getCommunityResult.Value.data.role == CommunityMemberRole.owner);
            viewInstance.SetCommunityDescription(getCommunityResult.Value.data.description);
            viewInstance.SetCommunityPrivacy(getCommunityResult.Value.data.privacy, getCommunityResult.Value.data.role == CommunityMemberRole.owner);

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
                    // Owners' names
                    using PoolExtensions.Scope<List<string>> userIds = USER_IDS_POOL.AutoScope();
                    foreach (var communityPlace in getPlacesDetailsResult.Value.data)
                    {
                        if (userIds.Value.Contains(communityPlace.owner))
                            continue;

                        userIds.Value.Add(communityPlace.owner);
                    }

                    var getAvatarsDetailsResult = await lambdasProfilesProvider.GetAvatarsDetailsAsync(userIds.Value, ct)
                                                                               .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    if (!getAvatarsDetailsResult.Success)
                    {
                        showErrorCts = showErrorCts.SafeRestart();
                        await viewInstance!.WarningNotificationView.AnimatedShowAsync(GET_OWNERS_NAMES_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    }

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

                        string ownerName = string.Empty;
                        if (getAvatarsDetailsResult.Success)
                        {
                            foreach (var avatarDetails in getAvatarsDetailsResult.Value)
                            {
                                if (avatarDetails.avatars.Count == 0 || !string.Equals(placeInfo.owner, avatarDetails.avatars[0].userId, StringComparison.CurrentCultureIgnoreCase))
                                    continue;

                                ownerName = avatarDetails.avatars[0].name;
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(placeInfo.world_name))
                            originalCommunityLandsForEdition.Add(placeInfo.id);
                        else
                            originalCommunityWorldsForEdition.Add(placeInfo.id);

                        AddPlaceTag(new CommunityPlace(
                            placeInfo.id,
                            !string.IsNullOrEmpty(placeInfo.world_name),
                            string.IsNullOrEmpty(placeInfo.world_name) ?
                                $"{placeInfo.title} ({placeInfo.base_position})" :
                                $"{placeInfo.title} ({placeInfo.world_name})",
                            placeInfo.owner,
                            ownerName), isRemovalAllowed, updateScrollPosition: false);
                    }
                }
            }
        }

        private void CreateCommunity(string name, string description, List<string> lands, List<string> worlds, CommunityPrivacy privacy)
        {
            viewInstance!.ShowComplianceErrorModal(false);
            createCommunityCts = createCommunityCts.SafeRestart();
            CreateCommunityAsync(name, description, lands, worlds, privacy, createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid CreateCommunityAsync(string name, string description, List<string> lands, List<string> worlds, CommunityPrivacy privacy, CancellationToken ct)
        {
            viewInstance!.SetCommunityCreationInProgress(true);

            var result = await dataProvider.CreateOrUpdateCommunityAsync(null, name, description, lastSelectedImageData, lands, worlds, privacy, ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES,
                                                exceptionToResult: exception =>
                                                {
                                                    if (exception is UnityWebRequestException { ResponseCode: WebRequestUtils.BAD_REQUEST })
                                                    {
                                                        return Result<CreateOrUpdateCommunityResponse>.SuccessResult(new CreateOrUpdateCommunityResponse
                                                        {
                                                            complianceStatus = CreateOrUpdateCommunityResponse.ComplianceStatus.REJECTED,
                                                        });
                                                    }

                                                    return Result.ErrorResult(exception.Message);
                                                });

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

            switch (result.Value.complianceStatus)
            {
                case CreateOrUpdateCommunityResponse.ComplianceStatus.APPROVED:
                    closeTaskCompletionSource.TrySetResult();
                    openCommunityCardAfterCreationCts = openCommunityCardAfterCreationCts.SafeRestart();
                    mvcManager.ShowAsync(CommunityCardController.IssueCommand(new CommunityCardParameter(result.Value.data.id, thumbnailLoader!.Cache)), openCommunityCardAfterCreationCts.Token).Forget();
                    break;
                case CreateOrUpdateCommunityResponse.ComplianceStatus.REJECTED:
                    // Show REJECTED modal
                    viewInstance.ShowComplianceErrorModal(true, errorMessage: "TODO...");
                    break;
                case CreateOrUpdateCommunityResponse.ComplianceStatus.VALIDATION_NOT_AVAILABLE:
                    // Show VALIDATION NOT AVAILABLE modal
                    viewInstance.ShowComplianceErrorModal(true, isApiAvailable: false);
                    break;
            }
        }

        private void UpdateCommunity(string name, string description, List<string> lands, List<string> worlds, CommunityPrivacy privacy)
        {
            viewInstance!.ShowComplianceErrorModal(false);
            createCommunityCts = createCommunityCts.SafeRestart();
            UpdateCommunityAsync(
                inputData.CommunityId,
                originalCommunityNameForEdition == name ? null : name,
                originalCommunityDescriptionForEdition == description ? null : description,
                originalCommunityLandsForEdition.Count == lands.Count && !originalCommunityLandsForEdition.Except(lands).Any() ? null : lands,
                originalCommunityWorldsForEdition.Count == worlds.Count && !originalCommunityWorldsForEdition.Except(worlds).Any() ? null : worlds,
                originalCommunityPrivacyForEdition == privacy ? null : privacy,
                createCommunityCts.Token).Forget();
        }

        private async UniTaskVoid UpdateCommunityAsync(string id, string? name, string? description, List<string>? lands, List<string>? worlds, CommunityPrivacy? privacy,
            CancellationToken ct)
        {
            if (name == null &&
                description == null &&
                lands == null &&
                worlds == null &&
                privacy == null &&
                lastSelectedImageData == null)
            {
                // If there is nothing to save, just close the panel
                closeTaskCompletionSource.TrySetResult();
                return;
            }

            viewInstance!.SetCommunityCreationInProgress(true);

            var result = await dataProvider.CreateOrUpdateCommunityAsync(id, name, description, lastSelectedImageData, lands, worlds, privacy, ct)
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
                thumbnailLoader!.Cache?.AddOrReplaceCachedSprite(result.Value.data.thumbnails?.raw, lastSelectedProfileThumbnail);
                isProfileThumbnailDirty = false;
                lastSelectedProfileThumbnail = null;
            }

            closeTaskCompletionSource.TrySetResult();
        }

        private void OpenContentPolicyAndCodeOfEthicsLink(string id)
        {
            switch (id)
            {
                case CONTENT_POLICY_LINK_ID:
                    webBrowser.OpenUrl(DecentralandUrl.ContentPolicy);
                    break;
                case CODE_AND_ETHICS_LINK_ID:
                    webBrowser.OpenUrl(DecentralandUrl.CodeOfEthics);
                    break;
            }

            viewInstance!.PlayOnLinkClickAudio();
        }

        private void GoBackToCreationEditionModal()
        {
            viewInstance!.SetCommunityCreationInProgress(false);
            viewInstance!.ShowComplianceErrorModal(false);
        }
    }
}
