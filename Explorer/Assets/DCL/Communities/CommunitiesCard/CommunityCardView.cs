using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Announcements;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesCard.Photos;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.Controls.Configs;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

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

        private const string REJECT_COMMUNITY_INVITATION_TEXT_FORMAT = "Are you sure you want to delete your invitation to the [{0}] Community?";
        private const string REJECT_COMMUNITY_INVITATION_CONFIRM_TEXT = "YES";
        private const string REJECT_COMMUNITY_INVITATION_CANCEL_TEXT = "NO";

        public enum Sections
        {
            PHOTOS,
            MEMBERS,
            PLACES,
            ANNOUNCEMENTS,
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
        public event Action? RequestToJoinCommunity;
        public event Action? CancelRequestToJoinCommunity;
        public event Action? AcceptInvite;
        public event Action? RejectInvite;

        [field: Header("References")]
        [field: SerializeField] private Button closeButton { get; set; } = null!;
        [field: SerializeField] private Button backgroundCloseButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingObject { get; set; } = null!;
        [field: SerializeField] private Image backgroundImage { get; set; } = null!;
        [field: SerializeField] public Color BackgroundColor { get; private set; }
        [field: SerializeField] private Sprite defaultCommunityImage { get; set; } = null!;
        [field: SerializeField] internal CommunityCardVoiceChatView communityCardVoiceChatView { get; set; } = null!;
        [field: SerializeField] private CommunityCardContextMenuConfiguration contextMenuSettings { get; set; } = null!;

        [field: Header("Community interactions")]
        [field: SerializeField] private Button openChatButton { get; set; } = null!;
        [field: SerializeField] private Button openWizardButton { get; set; } = null!;
        [field: SerializeField] private Button openContextMenuButton { get; set; } = null!;
        [field: SerializeField] private Button joinedButton { get; set; } = null!;
        [field: SerializeField] private Button joinButton { get; set; } = null!;
        [field: SerializeField] private Button requestToJoinButton { get; set; } = null!;
        [field: SerializeField] private Button cancelRequestButton { get; set; } = null!;
        [field: SerializeField] private Button acceptInviteButton { get; set; } = null!;
        [field: SerializeField] private Button rejectInviteButton { get; set; } = null!;

        [field: Header("Community data references")]
        [field: SerializeField] private TMP_Text communityName { get; set; } = null!;
        [field: SerializeField] private TMP_Text communityMembersNumber { get; set; } = null!;
        [field: SerializeField] private TMP_Text communityPrivacyText { get; set; } = null!;
        [field: SerializeField] private GameObject publicCommunityIcon { get; set; } = null!;
        [field: SerializeField] private GameObject privateCommunityIcon { get; set; } = null!;
        [field: SerializeField] private TMP_Text communityDescription { get; set; } = null!;
        [field: SerializeField] public ImageView CommunityThumbnail { get; private set; } = null!;
        [field: SerializeField] public GameObject UnlistedMark { get; private set; } = null!;
        [field: SerializeField] public GameObject UnlistedSeparator { get; private set; } = null!;

        [field: Header("-- Sections")]
        [field: Header("Buttons")]
        [field: SerializeField] private Button photosButton { get; set; } = null!;
        [field: SerializeField] private Button membersButton { get; set; } = null!;
        [field: SerializeField] private Button placesButton { get; set; } = null!;
        [field: SerializeField] private Button placesWithSignButton { get; set; } = null!;
        [field: SerializeField] private Button announcementsButton { get; set; } = null!;
        [field: SerializeField] private Button placesShortcutButton { get; set; } = null!;
        [field: SerializeField] private Button membersTextButton { get; set; } = null!;

        [field: Header("Selections")]
        [field: SerializeField] private GameObject photosSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject membersSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject placesSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject placesWithSignSectionSelection { get; set; } = null!;
        [field: SerializeField] private GameObject announcementsSelection { get; set; } = null!;

        [field: Header("Sections views")]
        [field: SerializeField] public CameraReelGalleryConfig CameraReelGalleryConfigs { get; private set; }
        [field: SerializeField] public MembersListView MembersListView { get; private set; } = null!;
        [field: SerializeField] public PlacesSectionView PlacesSectionView { get; private set; } = null!;
        [field: SerializeField] public EventListView EventListView { get; private set; } = null!;
        [field: SerializeField] public AnnouncementsSectionView AnnouncementsSectionView { get; private set; } = null!;

        [field: Header("Restricted Access")]
        [field: SerializeField] public List<GameObject> ObjectsToShowWhenAccessIsAllowed { get; private set; }
        [field: SerializeField] public List<GameObject> ObjectsToShowWhenAccessIsNotAllowed { get; private set; }

        private readonly UniTask[] closingTasks = new UniTask[6];

        private CancellationTokenSource confirmationDialogCts = new ();
        private GenericContextMenu? contextMenu;
        private GenericContextMenuElement? leaveCommunityContextMenuElement;
        private GenericContextMenuElement? deleteCommunityContextMenuElement;
        private CancellationToken cancellationToken;

        private void Awake()
        {
            openWizardButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());
            openChatButton.onClick.AddListener(() => OpenChatRequested?.Invoke());
            openContextMenuButton.onClick.AddListener(OpenContextMenu);
            joinButton.onClick.AddListener(() => JoinCommunity?.Invoke());
            joinedButton.onClick.AddListener(ShowLeaveConfirmationDialog);
            requestToJoinButton.onClick.AddListener(() => RequestToJoinCommunity?.Invoke());
            cancelRequestButton.onClick.AddListener(() => CancelRequestToJoinCommunity?.Invoke());
            acceptInviteButton.onClick.AddListener(() => AcceptInvite?.Invoke());
            rejectInviteButton.onClick.AddListener(() =>
            {
                confirmationDialogCts = confirmationDialogCts.SafeRestart();
                ShowDeleteInvitationConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
                return;

                async UniTask ShowDeleteInvitationConfirmationDialogAsync(CancellationToken ct)
                {
                    Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(REJECT_COMMUNITY_INVITATION_TEXT_FORMAT, communityName.text),
                                                                                         REJECT_COMMUNITY_INVITATION_CANCEL_TEXT,
                                                                                         REJECT_COMMUNITY_INVITATION_CONFIRM_TEXT,
                                                                                         CommunityThumbnail.ImageSprite,
                                                                                         false, false), ct)
                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                    RejectInvite?.Invoke();
                }
            });

            photosButton.onClick.AddListener(() => ToggleSection(Sections.PHOTOS));
            membersButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
            membersTextButton.onClick.AddListener(() => ToggleSection(Sections.MEMBERS));
            placesButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
            placesWithSignButton.onClick.AddListener(() => ToggleSection(Sections.PLACES));
            announcementsButton.onClick.AddListener(() => ToggleSection(Sections.ANNOUNCEMENTS));
            placesShortcutButton.onClick.AddListener(() => OpenWizardRequested?.Invoke());

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget,
                              verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              anchorPoint: ContextMenuOpenDirection.BOTTOM_LEFT)
                         .AddControl(leaveCommunityContextMenuElement = new GenericContextMenuElement(
                              new ButtonContextMenuControlSettings(contextMenuSettings.LeaveCommunityText, contextMenuSettings.LeaveCommunitySprite, ShowLeaveConfirmationDialog)))
                         .AddControl(deleteCommunityContextMenuElement = new GenericContextMenuElement(
                              new ButtonContextMenuControlSettings(contextMenuSettings.DeleteCommunityText, contextMenuSettings.DeleteCommunitySprite, OnDeleteCommunityRequested,
                                  textColor: contextMenuSettings.DeleteCommunityTextColor, iconColor: contextMenuSettings.DeleteCommunityTextColor)));
        }

        private void OnDeleteCommunityRequested()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowDeleteConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(DELETE_COMMUNITY_TEXT_FORMAT, communityName.text),
                                                                                     DELETE_COMMUNITY_CANCEL_TEXT,
                                                                                     DELETE_COMMUNITY_CONFIRM_TEXT,
                                                                                     CommunityThumbnail.ImageSprite,
                                                                                     true, false), ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                DeleteCommunityRequested?.Invoke();
            }
        }

        public void SetPanelCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        private void OpenContextMenu()
        {
            openContextMenuButton.interactable = false;

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, openContextMenuButton.transform.position,
                actionOnHide: () => openContextMenuButton.interactable = true), cancellationToken);
        }

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
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(LEAVE_COMMUNITY_TEXT_FORMAT, communityName.text),
                                                                                         LEAVE_COMMUNITY_CANCEL_TEXT,
                                                                                         LEAVE_COMMUNITY_CONFIRM_TEXT,
                                                                                         CommunityThumbnail.ImageSprite,
                                                                                         true, true),
                                                                                     ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                LeaveCommunityRequested?.Invoke();
            }
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = closeButton.OnClickAsync(ct);
            closingTasks[1] = backgroundCloseButton.OnClickAsync(ct);
            closingTasks[2] = controllerTask;
            closingTasks[3] = communityCardVoiceChatView.StartStreamButton.OnClickAsync(ct);
            closingTasks[4] = communityCardVoiceChatView.ListeningButton.OnClickAsync(ct);
            closingTasks[5] = communityCardVoiceChatView.JoinStreamButton.OnClickAsync(ct);

            return closingTasks;
        }

        public void SetCardBackgroundColor(Color color, int shaderProperty)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            backgroundImage.material.SetColor(shaderProperty, Color.HSVToRGB(h, s, Mathf.Clamp01(v - 0.3f)));
        }

        public void ResetToggle(bool invokeEvent) =>
            ToggleSection(Sections.ANNOUNCEMENTS, invokeEvent);

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
            announcementsSelection.SetActive(section == Sections.ANNOUNCEMENTS);

            CameraReelGalleryConfigs.PhotosView.SetActive(section == Sections.PHOTOS);
            MembersListView.SetActive(section == Sections.MEMBERS);
            PlacesSectionView.SetActive(section == Sections.PLACES);
            AnnouncementsSectionView.SetActive(section == Sections.ANNOUNCEMENTS);

            if (invokeEvent)
                SectionChanged?.Invoke(section);
        }

        public void ConfigureInteractionButtons(GetCommunityResponse.CommunityData communityData)
        {
            openChatButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator or CommunityMemberRole.member && communityData.IsAccessAllowed() && communityData.pendingActionType != InviteRequestAction.invite);
            openWizardButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator && communityData.IsAccessAllowed() && communityData.pendingActionType != InviteRequestAction.invite);
            openContextMenuButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator && communityData.IsAccessAllowed() && communityData.pendingActionType != InviteRequestAction.invite);
            joinedButton.gameObject.SetActive(communityData.role is CommunityMemberRole.member && communityData.IsAccessAllowed() && communityData.pendingActionType != InviteRequestAction.invite);
            joinButton.gameObject.SetActive(communityData.role == CommunityMemberRole.none && communityData.IsAccessAllowed() && communityData.pendingActionType != InviteRequestAction.invite);
            requestToJoinButton.gameObject.SetActive(!communityData.IsAccessAllowed() && communityData.pendingActionType == InviteRequestAction.none);
            cancelRequestButton.gameObject.SetActive(!communityData.IsAccessAllowed() && communityData.pendingActionType == InviteRequestAction.request_to_join);
            acceptInviteButton.gameObject.SetActive(communityData.pendingActionType == InviteRequestAction.invite);
            rejectInviteButton.gameObject.SetActive(communityData.pendingActionType == InviteRequestAction.invite);
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
            requestToJoinButton.gameObject.SetActive(false);
            cancelRequestButton.gameObject.SetActive(false);
            acceptInviteButton.gameObject.SetActive(false);
            rejectInviteButton.gameObject.SetActive(false);
            placesWithSignButton.gameObject.SetActive(false);
            placesButton.gameObject.SetActive(true);
            SetCommunityAccessAsAllowed(true);
        }

        public void UpdateMemberCount(GetCommunityResponse.CommunityData communityData) =>
            communityMembersNumber.text = string.Format(COMMUNITY_MEMBERS_NUMBER_FORMAT, CommunitiesUtility.NumberToCompactString(communityData.membersCount));

        public void ConfigureCommunity(GetCommunityResponse.CommunityData communityData,
            ThumbnailLoader thumbnailLoader)
        {
            communityName.text = communityData.name;
            UpdateMemberCount(communityData);
            communityDescription.text = communityData.description;
            communityPrivacyText.text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(communityData.privacy.ToString());
            UnlistedMark.SetActive(communityData.visibility == CommunityVisibility.unlisted);
            UnlistedSeparator.SetActive(communityData.visibility == CommunityVisibility.unlisted);

            publicCommunityIcon.SetActive(communityData.privacy == CommunityPrivacy.@public);
            privateCommunityIcon.SetActive(communityData.privacy == CommunityPrivacy.@private);

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(communityData.thumbnailUrl, CommunityThumbnail, defaultCommunityImage, cancellationToken, true).Forget();

            deleteCommunityContextMenuElement!.Enabled = communityData.role == CommunityMemberRole.owner;
            leaveCommunityContextMenuElement!.Enabled = communityData.role == CommunityMemberRole.moderator;

            ConfigureInteractionButtons(communityData);

            placesWithSignButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            placesButton.gameObject.SetActive(!placesWithSignButton.gameObject.activeSelf);

            SetCommunityAccessAsAllowed(communityData.IsAccessAllowed());
        }

        public void SetCommunityAccessAsAllowed(bool isAllowed)
        {
            foreach (GameObject go in ObjectsToShowWhenAccessIsAllowed)
                go.SetActive(isAllowed);

            foreach (GameObject go in ObjectsToShowWhenAccessIsNotAllowed)
                go.SetActive(!isAllowed);
        }
    }
}
