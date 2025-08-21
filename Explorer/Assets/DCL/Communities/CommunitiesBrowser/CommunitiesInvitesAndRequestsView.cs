using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesInvitesAndRequestsView : MonoBehaviour
    {
        private const int INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY = 5;

        public event Action? InvitesAndRequestsButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string, string, CommunityResultCardView>? RequestToJoinCommunityCanceled;
        public event Action<string, string, CommunityResultCardView>? CommunityInvitationAccepted;
        public event Action<string, string, CommunityResultCardView>? CommunityInvitationRejected;

        [Header("Invites & Requests Section")]
        [SerializeField] private Button invitesAndRequestsButton = null!;
        [SerializeField] private GameObject invitesCounterContainer = null!;
        [SerializeField] private TMP_Text invitesCounterText = null!;
        [SerializeField] private GameObject invitesAndRequestsSection = null!;
        [SerializeField] private GameObject invitesAndRequestsDataContainer = null!;
        [SerializeField] private GameObject invitesAndRequestsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView invitesAndRequestsLoadingSpinner = null!;
        [SerializeField] private CommunityResultCardView communityCardPrefab = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        [Header("Invites")]
        [SerializeField] private Transform invitesGridContainer = null!;
        [SerializeField] private GameObject invitesEmptyContainer = null!;
        [SerializeField] private TMP_Text invitesTitleText = null!;

        [Header("Requests")]
        [SerializeField] private Transform requestsGridContainer = null!;
        [SerializeField] private GameObject requestsEmptyContainer = null!;
        [SerializeField] private TMP_Text requestsTitleText = null!;

        private int currentInvitesCounter = 0;

        private IObjectPool<CommunityResultCardView> invitedCommunityCardsPool = null!;
        private IObjectPool<CommunityResultCardView> requestedToJoinCommunityCardsPool = null!;
        private readonly List<CommunityResultCardView> currentInvites = new ();
        private readonly List<CommunityResultCardView> currentRequests = new ();
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ThumbnailLoader? thumbnailLoader;
        private CancellationTokenSource thumbnailsCts = new ();

        private void Awake()
        {
            invitesAndRequestsButton.onClick.AddListener(() => InvitesAndRequestsButtonClicked?.Invoke());

            invitedCommunityCardsPool = new ObjectPool<CommunityResultCardView>(
                InstantiateInvitedCommunityCardPrefab,
                defaultCapacity: INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: invitedCommunityCardView => invitedCommunityCardView.gameObject.SetActive(true),
                actionOnRelease: invitedCommunityCardView => invitedCommunityCardView.gameObject.SetActive(false));

            requestedToJoinCommunityCardsPool = new ObjectPool<CommunityResultCardView>(
                InstantiateRequestedToJoinCommunityCardPrefab,
                defaultCapacity: INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: requestedToJoinCommunityCardView => requestedToJoinCommunityCardView.gameObject.SetActive(true),
                actionOnRelease: requestedToJoinCommunityCardView => requestedToJoinCommunityCardView.gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            invitesAndRequestsButton.onClick.RemoveAllListeners();
            thumbnailsCts.SafeCancelAndDispose();
        }

        public void Initialize(ProfileRepositoryWrapper profileDataProvider) =>
            profileRepositoryWrapper = profileDataProvider;

        public void SetAsLoading(bool isLoading)
        {
            if (isLoading)
                invitesAndRequestsLoadingSpinner.ShowLoading();
            else
                invitesAndRequestsLoadingSpinner.HideLoading();
        }

        public void SetSectionActive(bool isActive) =>
            invitesAndRequestsSection.SetActive(isActive);

        public void ClearInvitesItems()
        {
            foreach (var invitedCommunity in currentInvites)
                invitedCommunityCardsPool.Release(invitedCommunity);

            currentInvites.Clear();
            SetInvitesAsEmpty(true);
        }

        public void SetInvitesItems(GetUserInviteRequestData.UserInviteRequestData[] communities)
        {
            foreach (var community in communities)
                CreateAndSetupInviteCard(community);

            SetInvitesAsEmpty(communities.Length == 0);
        }

        public void SetInvitesTitle(string text) =>
            invitesTitleText.text = text;

        public void SetInvitesCounter(int count)
        {
            invitesCounterContainer.SetActive(count > 0);
            invitesCounterText.text = count.ToString();
            currentInvitesCounter = count;
        }

        public void ClearRequestsItems()
        {
            foreach (var requestedCommunity in currentRequests)
                requestedToJoinCommunityCardsPool.Release(requestedCommunity);

            currentRequests.Clear();
            SetRequestsAsEmpty(true);
        }

        public void SetRequestsItems(GetUserInviteRequestData.UserInviteRequestData[] communities)
        {
            foreach (var community in communities)
                CreateAndSetupRequestCard(community);

            SetRequestsAsEmpty(communities.Length == 0);
        }

        public void SetRequestsTitle(string text) =>
            requestsTitleText.text = text;

        public void SetThumbnailLoader(ThumbnailLoader loader) =>
            thumbnailLoader = loader;

        public void SetInvitesAndRequestsAsEmpty(bool isEmpty)
        {
            invitesAndRequestsEmptyContainer.SetActive(isEmpty);
            invitesAndRequestsDataContainer.SetActive(!isEmpty);
        }

        public void UpdateJoinRequestCancelled(string communityId, bool isSuccess)
        {
            foreach (CommunityResultCardView requestCard in currentRequests)
            {
                if (requestCard.CommunityId != communityId)
                    continue;

                if (isSuccess)
                {
                    requestedToJoinCommunityCardsPool.Release(requestCard);
                    currentRequests.Remove(requestCard);

                    if (currentInvites.Count == 0 && currentRequests.Count == 0)
                        SetInvitesAndRequestsAsEmpty(true);
                    else
                        SetRequestsAsEmpty(currentRequests.Count == 0);
                }
                else
                {
                    requestCard.SetActonLoadingActive(false);
                    ClearSelection();
                }

                break;
            }
        }

        public void UpdateCommunityInvitation(string communityId, bool isSuccess)
        {
            foreach (CommunityResultCardView invitationCard in currentInvites)
            {
                if (invitationCard.CommunityId != communityId)
                    continue;

                if (isSuccess)
                {
                    invitedCommunityCardsPool.Release(invitationCard);
                    currentInvites.Remove(invitationCard);
                    SetInvitesCounter(currentInvitesCounter - 1);

                    if (currentInvites.Count == 0 && currentRequests.Count == 0)
                        SetInvitesAndRequestsAsEmpty(true);
                    else
                        SetInvitesAsEmpty(currentInvites.Count == 0);
                }
                else
                {
                    invitationCard.SetActonLoadingActive(false);
                    ClearSelection();
                }

                break;
            }
        }

        private void SetInvitesAsEmpty(bool isEmpty)
        {
            invitesEmptyContainer.SetActive(isEmpty);
            invitesGridContainer.gameObject.SetActive(!isEmpty);
        }

        private void SetRequestsAsEmpty(bool isEmpty)
        {
            requestsEmptyContainer.SetActive(isEmpty);
            requestsGridContainer.gameObject.SetActive(!isEmpty);
        }

        private CommunityResultCardView InstantiateInvitedCommunityCardPrefab()
        {
            CommunityResultCardView invitedCommunityCardView = Instantiate(communityCardPrefab, invitesGridContainer);
            return invitedCommunityCardView;
        }

        private void CreateAndSetupInviteCard(GetUserInviteRequestData.UserInviteRequestData community)
        {
            CommunityResultCardView invitedCommunityCardView = invitedCommunityCardsPool.Get();

            // Setup card data
            invitedCommunityCardView.SetCommunityId(community.communityId);
            invitedCommunityCardView.SetTitle(community.name);
            invitedCommunityCardView.SetOwner(community.ownerName);
            invitedCommunityCardView.SetDescription(community.description);
            invitedCommunityCardView.SetPrivacy(community.privacy);
            invitedCommunityCardView.SetMembersCount(community.membersCount);
            invitedCommunityCardView.SetInviteOrRequestId(community.id);
            invitedCommunityCardView.SetActionButtonsType(community.privacy, community.type, community.role != CommunityMemberRole.none);
            invitedCommunityCardView.SetActonLoadingActive(false);
            thumbnailLoader!.LoadCommunityThumbnailAsync(community.thumbnails?.raw, invitedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token).Forget();

            // Setup card events
            invitedCommunityCardView.MainButtonClicked -= OnOpenCommunityProfile;
            invitedCommunityCardView.MainButtonClicked += OnOpenCommunityProfile;
            invitedCommunityCardView.ViewCommunityButtonClicked -= OnOpenCommunityProfile;
            invitedCommunityCardView.ViewCommunityButtonClicked += OnOpenCommunityProfile;
            invitedCommunityCardView.AcceptCommunityInvitationButtonClicked -= OnCommunityInvitationAccepted;
            invitedCommunityCardView.AcceptCommunityInvitationButtonClicked += OnCommunityInvitationAccepted;
            invitedCommunityCardView.RejectCommunityInvitationButtonClicked -= OnCommunityInvitationRejected;
            invitedCommunityCardView.RejectCommunityInvitationButtonClicked += OnCommunityInvitationRejected;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                invitedCommunityCardView.SetupMutualFriends(profileRepositoryWrapper, community);

            currentInvites.Add(invitedCommunityCardView);
        }

        private CommunityResultCardView InstantiateRequestedToJoinCommunityCardPrefab()
        {
            CommunityResultCardView requestedToJoinCommunityCardView = Instantiate(communityCardPrefab, requestsGridContainer);
            return requestedToJoinCommunityCardView;
        }

        private void CreateAndSetupRequestCard(GetUserInviteRequestData.UserInviteRequestData community)
        {
            CommunityResultCardView requestedCommunityCardView = requestedToJoinCommunityCardsPool.Get();

            // Setup card data
            requestedCommunityCardView.SetCommunityId(community.communityId);
            requestedCommunityCardView.SetTitle(community.name);
            requestedCommunityCardView.SetOwner(community.ownerName);
            requestedCommunityCardView.SetDescription(community.description);
            requestedCommunityCardView.SetPrivacy(community.privacy);
            requestedCommunityCardView.SetMembersCount(community.membersCount);
            requestedCommunityCardView.SetInviteOrRequestId(community.id);
            requestedCommunityCardView.SetActionButtonsType(community.privacy, community.type, community.role != CommunityMemberRole.none);
            requestedCommunityCardView.SetActonLoadingActive(false);
            thumbnailLoader!.LoadCommunityThumbnailAsync(community.thumbnails?.raw, requestedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token).Forget();

            // Setup card events
            requestedCommunityCardView.MainButtonClicked -= OnOpenCommunityProfile;
            requestedCommunityCardView.MainButtonClicked += OnOpenCommunityProfile;
            requestedCommunityCardView.ViewCommunityButtonClicked -= OnOpenCommunityProfile;
            requestedCommunityCardView.ViewCommunityButtonClicked += OnOpenCommunityProfile;
            requestedCommunityCardView.CancelRequestToJoinCommunityButtonClicked -= OnCommunityRequestToJoinCanceled;
            requestedCommunityCardView.CancelRequestToJoinCommunityButtonClicked += OnCommunityRequestToJoinCanceled;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                requestedCommunityCardView.SetupMutualFriends(profileRepositoryWrapper, community);

            currentRequests.Add(requestedCommunityCardView);
        }

        private void OnOpenCommunityProfile(string communityId) =>
            CommunityProfileOpened?.Invoke(communityId);

        private void OnCommunityRequestToJoinCanceled(string communityId, string requestId, CommunityResultCardView cardView) =>
            RequestToJoinCommunityCanceled?.Invoke(communityId, requestId, cardView);

        private void OnCommunityInvitationAccepted(string communityId, string invitationId, CommunityResultCardView cardView) =>
            CommunityInvitationAccepted?.Invoke(communityId, invitationId, cardView);

        private void OnCommunityInvitationRejected(string communityId, string invitationId, CommunityResultCardView cardView) =>
            CommunityInvitationRejected?.Invoke(communityId, invitationId, cardView);

        private void ClearSelection() =>
            EventSystem.current.SetSelectedGameObject(null);
    }
}
