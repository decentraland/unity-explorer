using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DG.Tweening;
using MVC;
using System;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Utility;
using CommunityData = DCL.Communities.CommunitiesDataProvider.DTOs.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityResultCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 45f;
        private const string DELETE_COMMUNITY_INVITATION_TEXT_FORMAT = "Are you sure you want to delete your invitation to the [{0}] Community?";
        private const string DELETE_COMMUNITY_INVITATION_CONFIRM_TEXT = "YES";
        private const string DELETE_COMMUNITY_INVITATION_CANCEL_TEXT = "NO";

        private const string PUBLIC_PRIVACY_TEXT = "Public";
        private const string PRIVATE_PRIVACY_TEXT = "Private";
        private const string MEMBERS_COUNTER_FORMAT = "{0} Members";

        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [SerializeField] private TMP_Text communityOwner = null!;
        [SerializeField] private TMP_Text communityDescription = null!;
        [SerializeField] private CanvasGroup communityDescriptionCanvasGroup = null!;
        [field: SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private Image communityPrivacyIcon = null!;
        [SerializeField] private Sprite publicPrivacySprite = null!;
        [SerializeField] private Sprite privatePrivacySprite = null!;
        [SerializeField] private TMP_Text communityPrivacyText = null!;
        [SerializeField] private GameObject communityMembersSeparator = null!;
        [SerializeField] private TMP_Text communityMembersCountText = null!;
        [SerializeField] private Button mainButton = null!;
        [SerializeField] private GameObject buttonsContainer = null!;
        [SerializeField] private GameObject joinOrViewButtonsContainer = null!;
        [SerializeField] private Button viewCommunityButton = null!;
        [SerializeField] private Button joinCommunityButton = null!;
        [SerializeField] private GameObject requestOrCancelToJoinButtonsContainer = null!;
        [SerializeField] private Button requestToJoinButton = null!;
        [SerializeField] private Button cancelJoinRequestButton = null!;
        [SerializeField] private GameObject acceptOrRejectInvitationButtonsContainer = null!;
        [SerializeField] private Button acceptInvitationButton = null!;
        [SerializeField] private Button rejectInvitationButton = null!;
        [SerializeField] private GameObject joinStreamButtonContainer = null!;
        [SerializeField] private Button joinStreamButton = null!;
        [SerializeField] private Button goToStreamButton = null!;
        [SerializeField] private GameObject actionLoadingSpinner = null!;
        [SerializeField] private ListenersCountView listenersCountView = null!;
        [SerializeField] private GameObject listeningTooltip = null!;

        [SerializeField] private MutualFriendsConfig mutualFriends;

        private readonly StringBuilder stringBuilder = new ();

        private CancellationTokenSource? confirmationDialogCts;

        private string currentCommunityName = null!;
        private string currentInviteOrRequestId = null!;
        private Tweener? descriptionTween;
        private Tweener? footerTween;

        private Tweener? headerTween;
        private Vector2 originalFooterSizeDelta;
        private Vector2 originalHeaderSizeDelta;
        private bool isMember;

        public string CommunityId { get; private set; } = null!;

        private void Awake()
        {
            viewCommunityButton.onClick.AddListener(() => ViewCommunityButtonClicked?.Invoke(CommunityId));
            joinCommunityButton.onClick.AddListener(() => JoinCommunityButtonClicked?.Invoke(CommunityId, this));
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(CommunityId));
            requestToJoinButton.onClick.AddListener(() => RequestToJoinCommunityButtonClicked?.Invoke(CommunityId, this));
            cancelJoinRequestButton.onClick.AddListener(() => CancelRequestToJoinCommunityButtonClicked?.Invoke(CommunityId, currentInviteOrRequestId, this));
            acceptInvitationButton.onClick.AddListener(() => AcceptCommunityInvitationButtonClicked?.Invoke(CommunityId, currentInviteOrRequestId, this));
            joinStreamButton.onClick.AddListener(OnJoinStreamClicked);
            goToStreamButton.onClick.AddListener(OnGoToStreamButtonClicked);

            rejectInvitationButton.onClick.AddListener(OnRejectInvitationClicked);

            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;
        }

        private void OnRejectInvitationClicked()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteInvitationConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowDeleteInvitationConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(DELETE_COMMUNITY_INVITATION_TEXT_FORMAT, currentCommunityName), DELETE_COMMUNITY_INVITATION_CANCEL_TEXT, DELETE_COMMUNITY_INVITATION_CONFIRM_TEXT, communityThumbnail.ImageSprite, true, false), ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL)
                    return;

                RejectCommunityInvitationButtonClicked?.Invoke(CommunityId, currentInviteOrRequestId, this);
            }
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        private void OnDestroy()
        {
            mainButton.onClick.RemoveAllListeners();
            viewCommunityButton.onClick.RemoveAllListeners();
            joinCommunityButton.onClick.RemoveAllListeners();
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData) =>
            PlayHoverExitAnimation();

        public event Action<string>? MainButtonClicked;
        public event Action<string>? ViewCommunityButtonClicked;
        public event Action<string, bool>? JoinStreamButtonClicked;
        public event Action<string>? GoToStreamButtonClicked;
        public event Action<string, CommunityResultCardView>? JoinCommunityButtonClicked;
        public event Action<string, CommunityResultCardView>? RequestToJoinCommunityButtonClicked;
        public event Action<string, string, CommunityResultCardView>? CancelRequestToJoinCommunityButtonClicked;
        public event Action<string, string, CommunityResultCardView>? AcceptCommunityInvitationButtonClicked;
        public event Action<string, string, CommunityResultCardView>? RejectCommunityInvitationButtonClicked;

        private void OnJoinStreamClicked()
        {
            JoinStreamButtonClicked?.Invoke(CommunityId, isMember);
        }

        private void OnGoToStreamButtonClicked()
        {
            GoToStreamButtonClicked?.Invoke(CommunityId);
        }

        public void SetCommunityData(string id, string title, string owner, string description, bool isMember)
        {
            CommunityId = id;
            communityTitle.text = title;
            currentCommunityName = title;
            communityOwner.text = owner;
            communityDescription.text = description;
            this.isMember = isMember;
        }

        public void ConfigureListeningTooltip()
        {
            listenersCountView.gameObject.SetActive(false);
            listeningTooltip.gameObject.SetActive(true);
        }

        public void ConfigureListenersCount(bool isActive, int listenersCount)
        {
            listeningTooltip.gameObject.SetActive(false);
            listenersCountView.gameObject.SetActive(isActive);

            stringBuilder.Clear();
            stringBuilder.Append(listenersCount);
            listenersCountView.ParticipantCount.text = stringBuilder.ToString();
        }

        public void SetPrivacy(CommunityPrivacy privacy)
        {
            communityPrivacyIcon.sprite = privacy == CommunityPrivacy.@public ? publicPrivacySprite : privatePrivacySprite;
            communityPrivacyText.text = privacy == CommunityPrivacy.@public ? PUBLIC_PRIVACY_TEXT : PRIVATE_PRIVACY_TEXT;
        }

        public void SetMembersCount(int memberCount)
        {
            bool showMembers = CommunitiesFeatureAccess.Instance.CanMembersCounterBeDisplayer();
            communityMembersSeparator.SetActive(showMembers);
            communityMembersCountText.gameObject.SetActive(showMembers);

            if (showMembers)
                communityMembersCountText.text = string.Format(MEMBERS_COUNTER_FORMAT, CommunitiesUtility.NumberToCompactString(memberCount));
        }

        public void SetInviteOrRequestId(string id) =>
            currentInviteOrRequestId = id;

        public void SetActionButtonsState(CommunityPrivacy privacy, InviteRequestAction type, bool isMember, bool isStreaming = false, bool hasJoined = false)
        {
            if (isStreaming)
                SetActionButtonsState(hasJoined ? ActionButtonsState.STREAMING_GOTO : ActionButtonsState.STREAMING_JOIN);
            else if ((privacy == CommunityPrivacy.@public || isMember) && type == InviteRequestAction.none)
                SetActionButtonsState(isMember ? ActionButtonsState.PUBLIC_VIEW : ActionButtonsState.PUBLIC_JOIN);
            else if (privacy == CommunityPrivacy.@private && !isMember && type != InviteRequestAction.invite)
                SetActionButtonsState(type == InviteRequestAction.request_to_join ? ActionButtonsState.PRIVATE_CANCEL_JOIN : ActionButtonsState.PRIVATE_REQUEST_JOIN);
            else if (type == InviteRequestAction.invite)
                SetActionButtonsState(ActionButtonsState.PRIVATE_WITH_INVITE);
        }

        private void SetActionButtonsState(ActionButtonsState buttonsState)
        {
            switch (buttonsState)
            {
                case ActionButtonsState.PUBLIC_JOIN:
                case ActionButtonsState.PUBLIC_VIEW:
                    SetPublicState(buttonsState, isActiveState: true);
                    SetStreamingState(buttonsState, isActiveState: false);
                    SetPrivateJoinRequestState(buttonsState, isActiveState: false);
                    SetPrivateInvitationState(isActiveState: false);
                    break;
                case ActionButtonsState.STREAMING_JOIN:
                case ActionButtonsState.STREAMING_GOTO:
                    SetPublicState(buttonsState, isActiveState: false);
                    SetStreamingState(buttonsState, isActiveState: true);
                    SetPrivateJoinRequestState(buttonsState, isActiveState: false);
                    SetPrivateInvitationState(isActiveState: false);
                    break;
                case ActionButtonsState.PRIVATE_REQUEST_JOIN:
                case ActionButtonsState.PRIVATE_CANCEL_JOIN:
                    SetPublicState(buttonsState, isActiveState: false);
                    SetStreamingState(buttonsState, isActiveState: false);
                    SetPrivateJoinRequestState(buttonsState, isActiveState: true);
                    SetPrivateInvitationState(isActiveState: false);
                    break;
                case ActionButtonsState.PRIVATE_WITH_INVITE:
                    SetPublicState(buttonsState, isActiveState: false);
                    SetStreamingState(buttonsState, isActiveState: false);
                    SetPrivateJoinRequestState(buttonsState, isActiveState: false);
                    SetPrivateInvitationState(isActiveState: true);
                    break;
            }
        }

        private void SetPublicState(ActionButtonsState buttonsState, bool isActiveState)
        {
            joinOrViewButtonsContainer.SetActive(isActiveState);
            if (!isActiveState) return;
            joinCommunityButton.gameObject.SetActive(buttonsState is ActionButtonsState.PUBLIC_JOIN);
            viewCommunityButton.gameObject.SetActive(buttonsState is ActionButtonsState.PUBLIC_VIEW);
        }

        private void SetPrivateJoinRequestState(ActionButtonsState buttonsState, bool isActiveState)
        {
            requestOrCancelToJoinButtonsContainer.SetActive(isActiveState);
            if (!isActiveState) return;
            requestToJoinButton.gameObject.SetActive(buttonsState is ActionButtonsState.PRIVATE_REQUEST_JOIN);
            cancelJoinRequestButton.gameObject.SetActive(buttonsState is ActionButtonsState.PRIVATE_CANCEL_JOIN);
        }

        private void SetPrivateInvitationState(bool isActiveState)
        {
            acceptOrRejectInvitationButtonsContainer.SetActive(isActiveState);
            if (!isActiveState) return;
            rejectInvitationButton.gameObject.SetActive(true);
            acceptInvitationButton.gameObject.SetActive(true);
        }

        private void SetStreamingState(ActionButtonsState buttonsState, bool isActiveState)
        {
            joinStreamButtonContainer.SetActive(isActiveState);
            if (!isActiveState) return;
            goToStreamButton.gameObject.SetActive(buttonsState is ActionButtonsState.STREAMING_GOTO);
            joinStreamButton.gameObject.SetActive(buttonsState is ActionButtonsState.STREAMING_JOIN);
        }



        public void SetActionLoadingActive(bool isActive)
        {
            actionLoadingSpinner.SetActive(isActive);
            buttonsContainer.SetActive(!isActive);
        }

        public void SetupMutualFriends(ProfileRepositoryWrapper profileDataProvider, CommunityData communityData)
        {
            for (var i = 0; i < mutualFriends.thumbnails.Length; i++)
            {
                bool friendExists = i < communityData.friends.Length;
                mutualFriends.thumbnails[i].root.SetActive(friendExists);
                if (!friendExists) continue;
                Profile.CompactInfo mutualFriend = communityData.friends[i];
                mutualFriends.thumbnails[i].picture.Setup(profileDataProvider, mutualFriend);
                bool isOfficial = OfficialWalletsHelper.Instance.IsOfficialWallet(mutualFriend.UserId);
                mutualFriends.thumbnails[i].profileNameTooltip.Setup(mutualFriend.Name, mutualFriend.HasClaimedName, isOfficial);

                if (mutualFriends.thumbnails[i].isPointerEventsSubscribed)
                    continue;

                int thumbnailIndex = i;
                Action pointerEnterAction = () => mutualFriends.thumbnails[thumbnailIndex].profileNameTooltip.gameObject.SetActive(true);
                mutualFriends.thumbnails[i].picture.PointerEnter -= pointerEnterAction;
                mutualFriends.thumbnails[i].picture.PointerEnter += pointerEnterAction;

                Action pointerExitAction = () => mutualFriends.thumbnails[thumbnailIndex].profileNameTooltip.gameObject.SetActive(false);
                mutualFriends.thumbnails[i].picture.PointerExit -= pointerExitAction;
                mutualFriends.thumbnails[i].picture.PointerExit += pointerExitAction;

                mutualFriends.thumbnails[i].isPointerEventsSubscribed = true;
            }
        }

        public void SetupMutualFriends(ProfileRepositoryWrapper profileDataProvider, GetUserInviteRequestData.UserInviteRequestData userInviteRequestData)
        {
            var communityFromInviteRequestData = new CommunityData
            {
                id = userInviteRequestData.communityId,
                thumbnailUrl = userInviteRequestData.thumbnailUrl,
                name = userInviteRequestData.name,
                description = userInviteRequestData.description,
                ownerAddress = userInviteRequestData.ownerAddress,
                ownerName = userInviteRequestData.ownerName,
                membersCount = userInviteRequestData.membersCount,
                privacy = userInviteRequestData.privacy,
                role = userInviteRequestData.role,
                friends = userInviteRequestData.friends,
                pendingActionType = userInviteRequestData.type,
                inviteOrRequestId = userInviteRequestData.id,
            };

            SetupMutualFriends(profileDataProvider, communityFromInviteRequestData);
        }

        private void PlayHoverAnimation()
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            headerTween = DOTween.To(() =>
                                          headerContainer.sizeDelta,
                                      newSizeDelta => headerContainer.sizeDelta = newSizeDelta,
                                      new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y - HOVER_ANIMATION_HEIGHT_TO_APPLY),
                                      HOVER_ANIMATION_DURATION)
                                 .SetEase(Ease.OutQuad);

            footerTween = DOTween.To(() =>
                                          footerContainer.sizeDelta,
                                      newSizeDelta => footerContainer.sizeDelta = newSizeDelta,
                                      new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y + HOVER_ANIMATION_HEIGHT_TO_APPLY),
                                      HOVER_ANIMATION_DURATION)
                                 .SetEase(Ease.OutQuad);

            descriptionTween = DOTween.To(() =>
                                               communityDescriptionCanvasGroup.alpha,
                                           newAlpha => communityDescriptionCanvasGroup.alpha = newAlpha,
                                           1f,
                                           HOVER_ANIMATION_DURATION)
                                      .SetEase(Ease.OutQuad);
        }

        private void PlayHoverExitAnimation(bool instant = false)
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            if (instant)
            {
                headerContainer.sizeDelta = originalHeaderSizeDelta;
                footerContainer.sizeDelta = originalFooterSizeDelta;
                communityDescriptionCanvasGroup.alpha = 0f;
            }
            else
            {
                headerTween = DOTween.To(() =>
                                              headerContainer.sizeDelta,
                                          x => headerContainer.sizeDelta = x,
                                          new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y),
                                          HOVER_ANIMATION_DURATION)
                                     .SetEase(Ease.OutQuad);

                footerTween = DOTween.To(() =>
                                              footerContainer.sizeDelta,
                                          x => footerContainer.sizeDelta = x,
                                          new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y),
                                          HOVER_ANIMATION_DURATION)
                                     .SetEase(Ease.OutQuad);

                descriptionTween = DOTween.To(() =>
                                                   communityDescriptionCanvasGroup.alpha,
                                               newAlpha => communityDescriptionCanvasGroup.alpha = newAlpha,
                                               0f,
                                               HOVER_ANIMATION_DURATION)
                                          .SetEase(Ease.OutQuad);
            }
        }

        [Serializable]
        internal struct MutualFriendsConfig
        {
            public MutualThumbnail[] thumbnails;

            [Serializable]
            public struct MutualThumbnail
            {
                public GameObject root;
                public ProfilePictureView picture;
                public ProfileNameTooltipView profileNameTooltip;

                internal bool isPointerEventsSubscribed;
            }
        }

        private enum ActionButtonsState
        {
            PUBLIC_JOIN,
            PUBLIC_VIEW,
            STREAMING_JOIN,
            STREAMING_GOTO,
            PRIVATE_REQUEST_JOIN,
            PRIVATE_CANCEL_JOIN,
            PRIVATE_WITH_INVITE,
            DEFAULT,
        }
    }
}
