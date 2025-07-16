using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesCard.Photos;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardView : ViewBase, IView
    {
        private const string COMMUNITY_MEMBERS_NUMBER_FORMAT = "<b>{0}</b> Members";

        private const string LEAVE_COMMUNITY_TEXT_FORMAT = "Are you sure you want to leave '{0}'?";
        private const string LEAVE_COMMUNITY_CONFIRM_TEXT = "YES";
        private const string LEAVE_COMMUNITY_CANCEL_TEXT = "NO";

        private const string DELETE_COMMUNITY_TEXT_FORMAT = "Are you sure you want to delete [{0}] Community?";
        private const string DELETE_COMMUNITY_CONFIRM_TEXT = "CONTINUE";
        private const string DELETE_COMMUNITY_CANCEL_TEXT = "CANCEL";

        public enum Sections
        {
            PHOTOS,
            MEMBERS,
            PLACES,
        }

        [Serializable]
        public struct CameraReelGalleryConfig
        {
            public PhotosView PhotosView;
            public int GridLayoutFixedColumnCount;
            public int ThumbnailHeight;
            public int ThumbnailWidth;
        }

        public event Action<Sections>? SectionChanged;
        public event Action? OpenWizardRequested;
        public event Action? OpenChatRequested;
        public event Action? JoinCommunity;
        public event Action? LeaveCommunityRequested;
        public event Action? DeleteCommunityRequested;
        public event Action<bool>? ToggleNotificationsRequested;

        [field: Header("References")]
        [field: SerializeField] private Button closeButton { get; set; } = null!;
        [field: SerializeField] private Button backgroundCloseButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingObject { get; set; } = null!;
        [field: SerializeField] private Image backgroundImage { get; set; } = null!;
        [field: SerializeField] public Color BackgroundColor { get; private set; }
        [field: SerializeField] private ConfirmationDialogView confirmationDialogView { get; set; } = null!;
        [field: SerializeField] internal WarningNotificationView warningNotificationView { get; set; } = null!;
        [field: SerializeField] internal WarningNotificationView successNotificationView { get; set; } = null!;
        [field: SerializeField] private Sprite defaultCommunityImage { get; set; } = null!;
        [field: SerializeField] private Sprite deleteCommunityImage { get; set; } = null!;
        [field: SerializeField] private CommunityCardContextMenuConfiguration contextMenuSettings { get; set; } = null!;

        [field: Header("Community interactions")]
        [field: SerializeField] private Button openChatButton { get; set; } = null!;
        [field: SerializeField] private Button openWizardButton { get; set; } = null!;
        [field: SerializeField] private Button openContextMenuButton { get; set; } = null!;
        [field: SerializeField] private Button joinedButton { get; set; } = null!;
        [field: SerializeField] private Button joinButton { get; set; } = null!;

        [field: Header("Community data references")]
        [field: SerializeField] private TMP_Text communityName { get; set; } = null!;
        [field: SerializeField] private TMP_Text communityMembersNumber { get; set; } = null!;
        [field: SerializeField] private TMP_Text communityDescription { get; set; } = null!;
        [field: SerializeField] public ImageView CommunityThumbnail { get; private set; } = null!;

        [field: Header("-- Sections")]
        [field: Header("Buttons")]
        [field: SerializeField] private Button photosButton { get; set; } = null!;
        [field: SerializeField] private Button membersButton { get; set; } = null!;
        [field: SerializeField] private Button placesButton { get; set; } = null!;
        [field: SerializeField] private Button placesWithSignButton { get; set; } = null!;
        [field: SerializeField] private Button placesShortcutButton { get; set; } = null!;
        [field: SerializeField] private Button membersTextButton { get; set; } = null!;

        [field: Header("Selections")]
        [field: SerializeField] private GameObject photosSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject membersSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject placesSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject placesWithSignSectionSelection { get; set; } = null!;

        [field: Header("Sections views")]
        [field: SerializeField] public CameraReelGalleryConfig CameraReelGalleryConfigs { get; private set; }
        [field: SerializeField] public MembersListView MembersListView { get; private set; } = null!;
        [field: SerializeField] public PlacesSectionView PlacesSectionView { get; private set; } = null!;
        [field: SerializeField] public EventListView EventListView { get; private set; } = null!;

        private readonly UniTask[] closingTasks = new UniTask[3];
        private CancellationTokenSource confirmationDialogCts = new ();
        private GenericContextMenu? contextMenu;
        private GenericContextMenuElement? leaveCommunityContextMenuElement;
        private GenericContextMenuElement? deleteCommunityContextMenuElement;
        private ToggleWithIconContextMenuControlSettings? communityNotificationsContextMenuElement;
        private CancellationToken cancellationToken;
        private bool notificationsEnabled;
        private UniTaskCompletionSource? contextMenuCloseTask;

        private void Awake()
        {
            openWizardButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());
            openChatButton.onClick.AddListener(() => OpenChatRequested?.Invoke());
            openContextMenuButton.onClick.AddListener(OpenContextMenu);
            joinButton.onClick.AddListener(() => JoinCommunity?.Invoke());
            joinedButton.onClick.AddListener(ShowLeaveConfirmationDialog);

            photosButton.onClick.AddListener(() => ToggleSection(Sections.PHOTOS));
            membersButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
            membersTextButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
            placesButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
            placesWithSignButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
            placesShortcutButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget,
                              verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              anchorPoint: ContextMenuOpenDirection.BOTTOM_LEFT)
                         .AddControl(communityNotificationsContextMenuElement = new ToggleWithIconContextMenuControlSettings(
                              contextMenuSettings.ToggleNotificationsSprite, contextMenuSettings.ToggleNotificationsText, toggle => ToggleNotificationsRequested?.Invoke(toggle)))
                         .AddControl(leaveCommunityContextMenuElement = new GenericContextMenuElement(
                              new ButtonContextMenuControlSettings(contextMenuSettings.LeaveCommunityText, contextMenuSettings.LeaveCommunitySprite, ShowLeaveConfirmationDialog)))
                         .AddControl(deleteCommunityContextMenuElement = new GenericContextMenuElement(
                              new ButtonContextMenuControlSettings(contextMenuSettings.DeleteCommunityText, contextMenuSettings.DeleteCommunitySprite, OnDeleteCommunityRequested,
                                  textColor: contextMenuSettings.DeleteCommunityTextColor, iconColor: contextMenuSettings.DeleteCommunityTextColor)));
        }

        public void SetConfirmationDialogDependencies(ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            confirmationDialogView.SetProfileRepository(profileRepositoryWrapper);
        }

        private void OnDeleteCommunityRequested()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowDeleteConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationDialogView.ConfirmationResult> dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                                                                                                                  new ConfirmationDialogView.DialogData(string.Format(DELETE_COMMUNITY_TEXT_FORMAT, communityName.text),
                                                                                                                      DELETE_COMMUNITY_CANCEL_TEXT,
                                                                                                                      DELETE_COMMUNITY_CONFIRM_TEXT,
                                                                                                                      deleteCommunityImage,
                                                                                                                      false, false),
                                                                                                                  ct)
                                                                                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                DeleteCommunityRequested?.Invoke();
            }
        }

        public void SetPanelCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        private void OpenContextMenu()
        {
            contextMenuCloseTask = new UniTaskCompletionSource();
            openContextMenuButton.interactable = false;
            communityNotificationsContextMenuElement!.SetInitialValue(notificationsEnabled);

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, openContextMenuButton.transform.position,
                actionOnHide: () => openContextMenuButton.interactable = true,
                closeTask: contextMenuCloseTask.Task), cancellationToken);
        }

        public void CloseContextMenu() =>
            contextMenuCloseTask?.TrySetResult();

        private void OnDisable()
        {
            confirmationDialogCts.SafeCancelAndDispose();
        }

        private void ShowLeaveConfirmationDialog()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowLeaveConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowLeaveConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationDialogView.ConfirmationResult> dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                                                                                                        new ConfirmationDialogView.DialogData(string.Format(LEAVE_COMMUNITY_TEXT_FORMAT, communityName.text),
                                                                                                            LEAVE_COMMUNITY_CANCEL_TEXT,
                                                                                                            LEAVE_COMMUNITY_CONFIRM_TEXT,
                                                                                                            CommunityThumbnail.ImageSprite,
                                                                                                            true, true),
                                                                                                        ct)
                                                                                                     .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                LeaveCommunityRequested?.Invoke();
            }
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = closeButton.OnClickAsync(ct);
            closingTasks[1] = backgroundCloseButton.OnClickAsync(ct);
            closingTasks[2] = controllerTask;

            return closingTasks;
        }

        public void SetCardBackgroundColor(Color color, int shaderProperty)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            backgroundImage.material.SetColor(shaderProperty, Color.HSVToRGB(h, s, Mathf.Clamp01(v - 0.3f)));
        }

        public void ResetToggle(bool invokeEvent) =>
            ToggleSection(Sections.MEMBERS, invokeEvent);

        public void SetLoadingState(bool isLoading)
        {
            communityName.enabled = !isLoading;
            communityMembersNumber.enabled = !isLoading;
            communityDescription.enabled = !isLoading;
            EventListView.SetLoadingStateActive(isLoading);

            if (isLoading)
                loadingObject.ShowLoading();
            else
                loadingObject.HideLoading();
        }

        private void ToggleSection(Sections section, bool invokeEvent = true)
        {
            photosSectionSelection.SetActive(section == Sections.PHOTOS);
            membersSectionSelection.SetActive(section == Sections.MEMBERS);
            placesSectionSelection.SetActive(section == Sections.PLACES);
            placesWithSignSectionSelection.SetActive(section == Sections.PLACES);

            CameraReelGalleryConfigs.PhotosView.SetActive(section == Sections.PHOTOS);
            MembersListView.SetActive(section == Sections.MEMBERS);
            PlacesSectionView.SetActive(section == Sections.PLACES);

            if (invokeEvent)
                SectionChanged?.Invoke(section);
        }

        public void ConfigureInteractionButtons(CommunityMemberRole role)
        {
            openChatButton.gameObject.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator or CommunityMemberRole.member);
            openWizardButton.gameObject.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            openContextMenuButton.gameObject.SetActive(role is not CommunityMemberRole.none);
            joinedButton.gameObject.SetActive(role is CommunityMemberRole.member);
            joinButton.gameObject.SetActive(role == CommunityMemberRole.none);
        }

        public void SetDefaults()
        {
            CommunityThumbnail.SetImage(defaultCommunityImage, true);
            communityName.text = string.Empty;
            communityMembersNumber.text = string.Empty;
            communityDescription.text = string.Empty;

            openChatButton.gameObject.SetActive(false);
            openWizardButton.gameObject.SetActive(false);
            openContextMenuButton.gameObject.SetActive(false);
            joinedButton.gameObject.SetActive(false);
            joinButton.gameObject.SetActive(false);
            placesWithSignButton.gameObject.SetActive(false);
            placesButton.gameObject.SetActive(true);
        }

        public void ConfigureCommunity(GetCommunityResponse.CommunityData communityData,
            ThumbnailLoader thumbnailLoader)
        {
            communityName.text = communityData.name;
            communityMembersNumber.text = string.Format(COMMUNITY_MEMBERS_NUMBER_FORMAT, CommunitiesUtility.NumberToCompactString(communityData.membersCount));
            communityDescription.text = communityData.description;
            notificationsEnabled = communityData.notifications;

            if (communityData.thumbnails != null)

            thumbnailLoader.LoadCommunityThumbnailAsync(communityData.thumbnails.Value.raw, CommunityThumbnail, defaultCommunityImage, cancellationToken).Forget();

            deleteCommunityContextMenuElement!.Enabled = communityData.role == CommunityMemberRole.owner;
            leaveCommunityContextMenuElement!.Enabled = communityData.role == CommunityMemberRole.moderator;

            ConfigureInteractionButtons(communityData.role);

            placesWithSignButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            placesButton.gameObject.SetActive(!placesWithSignButton.gameObject.activeSelf);
        }
    }
}
