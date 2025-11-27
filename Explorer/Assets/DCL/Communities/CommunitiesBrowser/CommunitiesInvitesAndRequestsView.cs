using DCL.Communities.CommunitiesCard.Members;
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
        private const string INVITES_RESULTS_TITLE = "Invites";
        private const string REQUESTS_RECEIVED_RESULTS_TITLE = "Requests Received";
        private const string REQUESTS_RESULTS_TITLE = "Requests Sent";
        private const int INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY = 5;

        public event Action? BackButtonClicked;
        public event Action? InvitesAndRequestsButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string, string, CommunityResultCardView>? RequestToJoinCommunityCanceled;
        public event Action<string, string, CommunityResultCardView>? CommunityInvitationAccepted;
        public event Action<string, string, CommunityResultCardView>? CommunityInvitationRejected;
        public event Action<ICommunityMemberData>? OpenProfilePassportRequested;
        public event Action<ICommunityMemberData>? OpenUserChatRequested;
        public event Action<ICommunityMemberData>? CallUserRequested;
        public event Action<ICommunityMemberData>? BlockUserRequested;
        public event Action<string, ICommunityMemberData, InviteRequestIntention>? ManageRequestReceivedRequested;

        [Header("Invites & Requests Section")]
        [SerializeField] private Button backButton = null!;
        [SerializeField] private Button invitesAndRequestsButton = null!;
        [SerializeField] private GameObject invitesCounterContainer = null!;
        [SerializeField] private TMP_Text invitesCounterText = null!;
        [SerializeField] private GameObject invitesAndRequestsSection = null!;
        [SerializeField] private GameObject invitesAndRequestsDataContainer = null!;
        [SerializeField] private GameObject invitesAndRequestsEmptyContainer = null!;
        [SerializeField] private SkeletonLoadingView invitesAndRequestsLoadingSpinner = null!;
        [SerializeField] private CommunityResultCardView communityCardPrefab = null!;
        [SerializeField] private CommunityRequestsReceivedGroupView requestsReceivedGroupPrefab = null!;
        [SerializeField] private Sprite defaultThumbnailSprite = null!;

        [Header("Invites")]
        [SerializeField] private Transform invitesGridContainer = null!;
        [SerializeField] private GameObject invitesEmptyContainer = null!;
        [SerializeField] private TMP_Text invitesTitleText = null!;

        [Header("Requests Received")]
        [SerializeField] private Transform requestsReceivedGridContainer = null!;
        [SerializeField] private GameObject requestsReceivedEmptyContainer = null!;
        [SerializeField] private TMP_Text requestsReceivedTitleText = null!;

        [Header("Requests Sent")]
        [SerializeField] private Transform requestsGridContainer = null!;
        [SerializeField] private GameObject requestsEmptyContainer = null!;
        [SerializeField] private TMP_Text requestsTitleText = null!;

        private readonly List<CommunityResultCardView> currentInvites = new ();
        private readonly List<KeyValuePair<CommunityRequestsReceivedGroupView, MemberListItemView[]>> currentRequestsReceivedGroups = new ();
        private readonly List<CommunityResultCardView> currentRequests = new ();
        private readonly CancellationTokenSource thumbnailsCts = new ();
        private IObjectPool<CommunityResultCardView> invitedCommunityCardsPool = null!;
        private IObjectPool<CommunityRequestsReceivedGroupView> requestReceivedGroupsPool = null!;
        private IObjectPool<CommunityResultCardView> requestedToJoinCommunityCardsPool = null!;

        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private CommunitiesDataProvider.CommunitiesDataProvider? communitiesDataProvider;
        private ThumbnailLoader? thumbnailLoader;

        private void Awake()
        {
            invitesAndRequestsButton.onClick.AddListener(OnInvitesAndRequestsButtonClicked);
            backButton.onClick.AddListener(OnBackButtonClicked);

            invitedCommunityCardsPool = new ObjectPool<CommunityResultCardView>(
                InstantiateInvitedCommunityCardPrefab,
                defaultCapacity: INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: invitedCommunityCardView => invitedCommunityCardView.gameObject.SetActive(true),
                actionOnRelease: invitedCommunityCardView => invitedCommunityCardView.gameObject.SetActive(false));

            requestReceivedGroupsPool = new ObjectPool<CommunityRequestsReceivedGroupView>(
                InstantiateRequestsReceivedGroupPrefab,
                defaultCapacity: INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: requestsReceivedGroupView => requestsReceivedGroupView.gameObject.SetActive(true),
                actionOnRelease: requestsReceivedGroupView => requestsReceivedGroupView.gameObject.SetActive(false));

            requestedToJoinCommunityCardsPool = new ObjectPool<CommunityResultCardView>(
                InstantiateRequestedToJoinCommunityCardPrefab,
                defaultCapacity: INVITES_AND_REQUESTS_COMMUNITY_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: requestedToJoinCommunityCardView => requestedToJoinCommunityCardView.gameObject.SetActive(true),
                actionOnRelease: requestedToJoinCommunityCardView => requestedToJoinCommunityCardView.gameObject.SetActive(false));
        }

        private void OnInvitesAndRequestsButtonClicked()
        {
            InvitesAndRequestsButtonClicked?.Invoke();
        }

        private void OnBackButtonClicked()
        {
            BackButtonClicked?.Invoke();
        }

        private void OnDestroy()
        {
            invitesAndRequestsButton.onClick.RemoveAllListeners();
            backButton.onClick.RemoveAllListeners();
            thumbnailsCts.SafeCancelAndDispose();
        }

        public void Initialize(ProfileRepositoryWrapper profileDataProvider, CommunitiesDataProvider.CommunitiesDataProvider commDataProvider)
        {
            profileRepositoryWrapper = profileDataProvider;
            communitiesDataProvider = commDataProvider;
        }

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

        public void SetInvitesGridCounter(int count) =>
            invitesTitleText.text = $"{INVITES_RESULTS_TITLE} ({count})";

        public void SetInvitesCounter(int count)
        {
            invitesCounterContainer.SetActive(count > 0);
            invitesCounterText.text = count.ToString();
        }

        public void ClearRequestsReceivedItems()
        {
            foreach (var requestsReceivedGroup in currentRequestsReceivedGroups)
            {
                requestsReceivedGroup.Key.ClearRequestReceivedMemberItems();
                requestReceivedGroupsPool.Release(requestsReceivedGroup.Key);
            }

            currentRequestsReceivedGroups.Clear();
            SetRequestsReceivedAsEmpty(true);
        }

        public void SetRequestsReceivedItems(List<KeyValuePair<GetUserCommunitiesData.CommunityData, ICommunityMemberData[]>> requestsReceivedGroups)
        {
            foreach (var requestsReceivedGroup in requestsReceivedGroups)
                CreateAndSetupRequestsReceivedGroup(requestsReceivedGroup);

            SetRequestsReceivedAsEmpty(requestsReceivedGroups.Count == 0);
        }

        public void SetRequestsReceivedGridCounter(int count) =>
            requestsReceivedTitleText.text = $"{REQUESTS_RECEIVED_RESULTS_TITLE} ({count})";

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

        public void SetRequestsGridCounter(int count) =>
            requestsTitleText.text = $"{REQUESTS_RESULTS_TITLE} ({count})";

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

                    if (currentInvites.Count == 0 && currentRequestsReceivedGroups.Count == 0 && currentRequests.Count == 0)
                        SetInvitesAndRequestsAsEmpty(true);
                    else
                        SetRequestsAsEmpty(currentRequests.Count == 0);

                    SetRequestsGridCounter(currentRequests.Count);
                }
                else
                {
                    requestCard.SetActionLoadingActive(false);
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

                    if (currentInvites.Count == 0 && currentRequestsReceivedGroups.Count == 0 && currentRequests.Count == 0)
                        SetInvitesAndRequestsAsEmpty(true);
                    else
                        SetInvitesAsEmpty(currentInvites.Count == 0);

                    SetInvitesCounter(currentInvites.Count);
                }
                else
                {
                    invitationCard.SetActionLoadingActive(false);
                    ClearSelection();
                }

                break;
            }
        }

        public void UpdateRequestsReceived(string communityId, string profileId, bool isSuccess)
        {
            var currentRequestsReceivedGroupsIndex = 0;
            foreach (KeyValuePair<CommunityRequestsReceivedGroupView, MemberListItemView[]> requestsReceivedGroup in currentRequestsReceivedGroups)
            {
                if (requestsReceivedGroup.Key.CommunityId != communityId)
                {
                    currentRequestsReceivedGroupsIndex++;
                    continue;
                }

                if (isSuccess)
                {
                    int amountOfRequestReceivedMemberInGroup = requestsReceivedGroup.Key.UpdateRequestReceivedMember(profileId, isSuccess);
                    if (amountOfRequestReceivedMemberInGroup == 0)
                    {
                        requestReceivedGroupsPool.Release(requestsReceivedGroup.Key);
                        currentRequestsReceivedGroups.RemoveAt(currentRequestsReceivedGroupsIndex);
                    }
                    else
                    {
                        //currentRequestsReceivedGroups[currentRequestsReceivedGroupsIndex].Value
                    }

                    requestsReceivedGroup.Key.SetRequestsReceived(amountOfRequestReceivedMemberInGroup);

                    if (currentInvites.Count == 0 && currentRequestsReceivedGroups.Count == 0 && currentRequests.Count == 0)
                        SetInvitesAndRequestsAsEmpty(true);
                    else
                        SetRequestsReceivedAsEmpty(currentRequestsReceivedGroups.Count == 0);

                    var amountOfRequestReceivedMemberInAllGroups = 0;
                    foreach (var group in currentRequestsReceivedGroups)
                        amountOfRequestReceivedMemberInAllGroups += group.Value.Length;

                    SetRequestsReceivedGridCounter(amountOfRequestReceivedMemberInAllGroups);
                }
                else
                    ClearSelection();

                break;
            }
        }

        private void SetInvitesAsEmpty(bool isEmpty)
        {
            invitesEmptyContainer.SetActive(isEmpty);
            invitesGridContainer.gameObject.SetActive(!isEmpty);
        }

        private void SetRequestsReceivedAsEmpty(bool isEmpty)
        {
            requestsReceivedEmptyContainer.SetActive(isEmpty);
            requestsReceivedGridContainer.gameObject.SetActive(!isEmpty);
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

            bool isMember = community.role != CommunityMemberRole.none;

            // Setup card data
            invitedCommunityCardView.SetCommunityData(community.communityId, community.name, community.ownerName, community.description, isMember);
            invitedCommunityCardView.SetPrivacy(community.privacy);
            invitedCommunityCardView.SetMembersCount(community.membersCount);
            invitedCommunityCardView.SetInviteOrRequestId(community.id);
            invitedCommunityCardView.SetActionButtonsState(community.privacy, community.type, community.role != CommunityMemberRole.none);
            invitedCommunityCardView.SetActionLoadingActive(false);
            invitedCommunityCardView.ConfigureListenersCount(false, 0);
            thumbnailLoader!.LoadCommunityThumbnailFromUrlAsync(community.thumbnailUrl, invitedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token, true).Forget();

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

        private CommunityRequestsReceivedGroupView InstantiateRequestsReceivedGroupPrefab()
        {
            CommunityRequestsReceivedGroupView requestsReceivedGroup = Instantiate(requestsReceivedGroupPrefab, requestsReceivedGridContainer);
            return requestsReceivedGroup;
        }

        private void CreateAndSetupRequestsReceivedGroup(KeyValuePair<GetUserCommunitiesData.CommunityData, ICommunityMemberData[]> requestsReceivedGroup)
        {
            CommunityRequestsReceivedGroupView requestsReceivedGroupView = requestReceivedGroupsPool.Get();

            // Setup card data
            requestsReceivedGroupView.InitializePools();
            requestsReceivedGroupView.SetCommunityId(requestsReceivedGroup.Key.id);
            requestsReceivedGroupView.SetTitle(requestsReceivedGroup.Key.name);
            requestsReceivedGroupView.SetRequestsReceived(requestsReceivedGroup.Key.requestsReceived);
            requestsReceivedGroupView.SetProfileDataProvider(profileRepositoryWrapper!);
            requestsReceivedGroupView.SetCommunitiesDataProvider(communitiesDataProvider!);

            thumbnailLoader!.LoadCommunityThumbnailFromUrlAsync(requestsReceivedGroup.Key.thumbnailUrl, requestsReceivedGroupView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token, true).Forget();

            // Setup card events
            requestsReceivedGroupView.CommunityButtonClicked -= OnOpenCommunityProfile;
            requestsReceivedGroupView.CommunityButtonClicked += OnOpenCommunityProfile;
            requestsReceivedGroupView.OpenProfilePassportRequested -= OnOpenProfilePassport;
            requestsReceivedGroupView.OpenProfilePassportRequested += OnOpenProfilePassport;
            requestsReceivedGroupView.OpenUserChatRequested -= OnOpenUserChat;
            requestsReceivedGroupView.OpenUserChatRequested += OnOpenUserChat;
            requestsReceivedGroupView.CallUserRequested -= OnCallUser;
            requestsReceivedGroupView.CallUserRequested += OnCallUser;
            requestsReceivedGroupView.BlockUserRequested -= OnBlockUser;
            requestsReceivedGroupView.BlockUserRequested += OnBlockUser;
            requestsReceivedGroupView.RequestReceivedManageButtonClicked -= OnManageRequestReceived;
            requestsReceivedGroupView.RequestReceivedManageButtonClicked += OnManageRequestReceived;

            currentRequestsReceivedGroups.Add(
                new KeyValuePair<CommunityRequestsReceivedGroupView, MemberListItemView[]>(
                    requestsReceivedGroupView,
                    requestsReceivedGroupView.SetRequestReceivedMemberItems(requestsReceivedGroup.Value)));
        }

        private CommunityResultCardView InstantiateRequestedToJoinCommunityCardPrefab()
        {
            CommunityResultCardView requestedToJoinCommunityCardView = Instantiate(communityCardPrefab, requestsGridContainer);
            return requestedToJoinCommunityCardView;
        }

        private void CreateAndSetupRequestCard(GetUserInviteRequestData.UserInviteRequestData community)
        {
            CommunityResultCardView requestedCommunityCardView = requestedToJoinCommunityCardsPool.Get();

            bool isMember = community.role != CommunityMemberRole.none;

            // Setup card data
            requestedCommunityCardView.SetCommunityData(community.communityId, community.name, community.ownerName, community.description, isMember);
            requestedCommunityCardView.SetPrivacy(community.privacy);
            requestedCommunityCardView.SetMembersCount(community.membersCount);
            requestedCommunityCardView.SetInviteOrRequestId(community.id);
            requestedCommunityCardView.SetActionButtonsState(community.privacy, community.type, community.role != CommunityMemberRole.none);
            requestedCommunityCardView.SetActionLoadingActive(false);
            thumbnailLoader!.LoadCommunityThumbnailFromUrlAsync(community.thumbnailUrl, requestedCommunityCardView.communityThumbnail, defaultThumbnailSprite, thumbnailsCts.Token, true).Forget();

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

        private void OnOpenProfilePassport(ICommunityMemberData profile) =>
            OpenProfilePassportRequested?.Invoke(profile);

        private void OnOpenUserChat(ICommunityMemberData profile) =>
            OpenUserChatRequested?.Invoke(profile);

        private void OnCallUser(ICommunityMemberData profile) =>
            CallUserRequested?.Invoke(profile);

        private void OnBlockUser(ICommunityMemberData profile) =>
            BlockUserRequested?.Invoke(profile);

        private void OnManageRequestReceived(string communityId, ICommunityMemberData profile, InviteRequestIntention intention) =>
            ManageRequestReceivedRequested?.Invoke(communityId, profile, intention);
    }
}
