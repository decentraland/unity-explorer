using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesCard.Photos;
using DCL.Communities.CommunitiesCard.Places;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.UI;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardView : ViewBase, IView
    {
        private const string COMMUNITY_MEMBERS_NUMBER_FORMAT = "<b>{0}</b> members";

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

        [field: Header("References")]
        [field: SerializeField] private Button closeButton { get; set; }
        [field: SerializeField] private Button backgroundCloseButton { get; set; }
        [field: SerializeField] private SectionLoadingView loadingObject { get; set; }
        [field: SerializeField] private Image backgroundImage { get; set; }
        [field: SerializeField] public Color BackgroundColor { get; private set; }
        [field: SerializeField] private ConfirmationDialogView confirmationDialogView { get; set; }
        [field: SerializeField] private GameObject headerObject { get; set; }
        [field: SerializeField] private GameObject contentObject { get; set; }
        [field: SerializeField] internal WarningNotificationView warningNotificationView { get; set; }
        [field: SerializeField] internal WarningNotificationView successNotificationView { get; set; }
        [field: SerializeField] private Sprite defaultCommunityImage { get; set; }
        [field: SerializeField] private Sprite deleteCommunityImage { get; set; }
        [field: SerializeField] private CommunityCardContextMenuConfiguration contextMenuSettings { get; set; }

        [field: Header("Community interactions")]
        [field: SerializeField] private Button openChatButton { get; set; }
        [field: SerializeField] private Button openWizardButton { get; set; }
        [field: SerializeField] private Button openContextMenuButton { get; set; }
        [field: SerializeField] private Button joinedButton { get; set; }
        [field: SerializeField] private Button joinButton { get; set; }

        [field: Header("Community data references")]
        [field: SerializeField] private TMP_Text communityName { get; set; }
        [field: SerializeField] private TMP_Text communityMembersNumber { get; set; }
        [field: SerializeField] private TMP_Text communityDescription { get; set; }
        [field: SerializeField] public ImageView CommunityThumbnail { get; private set; }

        [field: Header("-- Sections")]
        [field: Header("Buttons")]
        [field: SerializeField] private Button photosButton { get; set; }
        [field: SerializeField] private Button membersButton { get; set; }
        [field: SerializeField] private Button placesButton { get; set; }
        [field: SerializeField] private Button placesWithSignButton { get; set; }
        [field: SerializeField] private Button membersTextButton { get; set; }

        [field: Header("Selections")]
        [field: SerializeField] private GameObject photosSectionSelection { get; set; }
        [field: SerializeField] private GameObject membersSectionSelection { get; set; }
        [field: SerializeField] private GameObject placesSectionSelection { get; set; }
        [field: SerializeField] private GameObject placesWithSignSectionSelection { get; set; }

        [field: Header("Sections views")]
        [field: SerializeField] public CameraReelGalleryConfig CameraReelGalleryConfigs { get; private set; }
        [field: SerializeField] public MembersListView MembersListView { get; private set; }
        [field: SerializeField] public PlacesSectionView PlacesSectionView { get; private set; }
        [field: SerializeField] public EventListView EventListView { get; private set; }

        private readonly UniTask[] closingTasks = new UniTask[3];
        private CancellationTokenSource confirmationDialogCts = new ();
        private GenericContextMenu contextMenu;
        private GenericContextMenuElement deleteCommunityContextMenuElement;
        private IMVCManager mvcManager;
        private CancellationToken cancellationToken;

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

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth,
                              offsetFromTarget: contextMenuSettings.OffsetFromTarget,
                              verticalLayoutPadding: contextMenuSettings.VerticalPadding,
                              elementsSpacing: contextMenuSettings.ElementsSpacing,
                              anchorPoint: ContextMenuOpenDirection.BOTTOM_LEFT)
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.LeaveCommunityText, contextMenuSettings.LeaveCommunitySprite, () => LeaveCommunityRequested?.Invoke()))
                         .AddControl(deleteCommunityContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.DeleteCommunityText, contextMenuSettings.DeleteCommunitySprite, OnDeleteCommunityRequested)));
        }

        private void OnDeleteCommunityRequested()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowDeleteConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(DELETE_COMMUNITY_TEXT_FORMAT, communityName.text),
                        DELETE_COMMUNITY_CANCEL_TEXT,
                        DELETE_COMMUNITY_CONFIRM_TEXT,
                        deleteCommunityImage,
                        false, false),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                DeleteCommunityRequested?.Invoke();
            }
        }

        public void ConfigureContextMenu(IMVCManager mvcManager, CancellationToken cancellationToken)
        {
            this.mvcManager = mvcManager;
            this.cancellationToken = cancellationToken;
        }

        private void OpenContextMenu()
        {
            openContextMenuButton.interactable = false;

            mvcManager.ShowAndForget(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, openContextMenuButton.transform.position,
                actionOnHide: () => openContextMenuButton.interactable = true)), cancellationToken);
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

            async UniTaskVoid ShowLeaveConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(LEAVE_COMMUNITY_TEXT_FORMAT, communityName.text),
                        LEAVE_COMMUNITY_CANCEL_TEXT,
                        LEAVE_COMMUNITY_CONFIRM_TEXT,
                        CommunityThumbnail.ImageSprite,
                        true, true),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

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

        public void ResetToggle() =>
            ToggleSection(Sections.MEMBERS);

        public void SetLoadingState(bool isLoading)
        {
            headerObject.SetActive(!isLoading);
            contentObject.SetActive(!isLoading);
            EventListView.SetLoadingStateActive(isLoading);

            if (isLoading)
                loadingObject?.Show();
            else
                loadingObject?.Hide();
        }

        private void ToggleSection(Sections section)
        {
            photosSectionSelection.SetActive(section == Sections.PHOTOS);
            membersSectionSelection.SetActive(section == Sections.MEMBERS);
            placesSectionSelection.SetActive(section == Sections.PLACES);
            placesWithSignSectionSelection.SetActive(section == Sections.PLACES);

            CameraReelGalleryConfigs.PhotosView.SetActive(section == Sections.PHOTOS);
            MembersListView.SetActive(section == Sections.MEMBERS);
            PlacesSectionView.SetActive(section == Sections.PLACES);

            SectionChanged?.Invoke(section);
        }

        public void ConfigureInteractionButtons(CommunityMemberRole role)
        {
            openChatButton.gameObject.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator or CommunityMemberRole.member);
            openWizardButton.gameObject.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            openContextMenuButton.gameObject.SetActive(role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            joinedButton.gameObject.SetActive(role is CommunityMemberRole.member);
            joinButton.gameObject.SetActive(role == CommunityMemberRole.none);
        }

        public void ConfigureCommunity(GetCommunityResponse.CommunityData communityData,
            ImageController imageController)
        {
            communityName.text = communityData.name;
            communityMembersNumber.text = string.Format(COMMUNITY_MEMBERS_NUMBER_FORMAT, CommunitiesUtility.NumberToCompactString(communityData.membersCount));
            communityDescription.text = communityData.description;

            imageController.SetImage(defaultCommunityImage);

            if (communityData.thumbnails != null)
                imageController.RequestImage(communityData.thumbnails.Value.raw, true, true);

            deleteCommunityContextMenuElement.Enabled = communityData.role == CommunityMemberRole.owner;

            ConfigureInteractionButtons(communityData.role);

            placesWithSignButton.gameObject.SetActive(communityData.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            placesButton.gameObject.SetActive(!placesWithSignButton.gameObject.activeSelf);
        }
    }
}
