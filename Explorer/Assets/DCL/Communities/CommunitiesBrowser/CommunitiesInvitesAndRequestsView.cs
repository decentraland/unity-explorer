using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesInvitesAndRequestsView : MonoBehaviour
    {
        private const int INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY = 5;

        public event Action? InvitesAndRequestsButtonClicked;
        public event Action<string>? CommunityProfileOpened;

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
            thumbnailsCts.Cancel();
            thumbnailsCts.Dispose();
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
            invitedCommunityCardView.SetActionButtonsType(community.privacy, community.type, community.role != CommunityMemberRole.none);
            invitedCommunityCardView.SetActonLoadingActive(false);
            thumbnailLoader!.LoadCommunityThumbnailAsync(community.thumbnails?.raw, invitedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token).Forget();

            // Setup card events
            invitedCommunityCardView.MainButtonClicked -= OnOpenCommunityProfile;
            invitedCommunityCardView.MainButtonClicked += OnOpenCommunityProfile;
            invitedCommunityCardView.ViewCommunityButtonClicked -= OnOpenCommunityProfile;
            invitedCommunityCardView.ViewCommunityButtonClicked += OnOpenCommunityProfile;

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
            // TODO (Santi): Create a pool!
            CommunityResultCardView requestedCommunityCardView = requestedToJoinCommunityCardsPool.Get();

            // Setup card data
            requestedCommunityCardView.SetCommunityId(community.communityId);
            requestedCommunityCardView.SetTitle(community.name);
            requestedCommunityCardView.SetOwner(community.ownerName);
            requestedCommunityCardView.SetDescription(community.description);
            requestedCommunityCardView.SetPrivacy(community.privacy);
            requestedCommunityCardView.SetMembersCount(community.membersCount);
            requestedCommunityCardView.SetActionButtonsType(community.privacy, community.type, community.role != CommunityMemberRole.none);
            requestedCommunityCardView.SetActonLoadingActive(false);
            thumbnailLoader!.LoadCommunityThumbnailAsync(community.thumbnails?.raw, requestedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token).Forget();

            // Setup card events
            requestedCommunityCardView.MainButtonClicked -= OnOpenCommunityProfile;
            requestedCommunityCardView.MainButtonClicked += OnOpenCommunityProfile;
            requestedCommunityCardView.ViewCommunityButtonClicked -= OnOpenCommunityProfile;
            requestedCommunityCardView.ViewCommunityButtonClicked += OnOpenCommunityProfile;

            // Setup mutual friends
            if (profileRepositoryWrapper != null)
                requestedCommunityCardView.SetupMutualFriends(profileRepositoryWrapper, community);

            currentRequests.Add(requestedCommunityCardView);
        }

        private void OnOpenCommunityProfile(string communityId) =>
            CommunityProfileOpened?.Invoke(communityId);
    }
}
