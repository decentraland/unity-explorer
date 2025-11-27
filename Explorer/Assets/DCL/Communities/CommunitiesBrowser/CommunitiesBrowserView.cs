using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        public event Action<string>? SearchBarSelected;
        public event Action<string>? SearchBarDeselected;
        public event Action<string>? SearchBarValueChanged;
        public event Action<string>? SearchBarSubmit;
        public event Action? SearchBarClearButtonClicked;
        public event Action<string>? CommunityProfileOpened;
        public event Action<string, string>? CommunityRequestToJoinCanceled;
        public event Action<string, string>? CommunityInvitationAccepted;
        public event Action<string, string>? CommunityInvitationRejected;
        public event Action? CreateCommunityButtonClicked;
        public event Action<ICommunityMemberData>? OpenProfilePassportRequested;
        public event Action<ICommunityMemberData>? OpenUserChatRequested;

        public MyCommunitiesView MyCommunitiesView => myCommunitiesView;
        public CommunitiesBrowserRightSectionMainView RightSectionView => rightSectionView;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        [Header("Search")]
        [SerializeField] private SearchBarView searchBar = null!;

        [Header("Creation Section")]
        [SerializeField] private Button createCommunityButton = null!;

        [Header("My Communities Section")]
        [SerializeField] private MyCommunitiesView myCommunitiesView = null!;

        [Header("Right Side Main Section")]
        [SerializeField] private CommunitiesBrowserRightSectionMainView rightSectionView = null!;

        [Header("Invites & Requests Section")]
        [SerializeField] private CommunitiesInvitesAndRequestsView invitesAndRequestsView = null!;

        public CommunitiesInvitesAndRequestsView InvitesAndRequestsView => invitesAndRequestsView;

        private ProfileRepositoryWrapper? profileRepositoryWrapper;

        private void Awake()
        {
            searchBar.inputField.onSelect.AddListener(text => SearchBarSelected?.Invoke(text));
            searchBar.inputField.onDeselect.AddListener(text => SearchBarDeselected?.Invoke(text));
            searchBar.inputField.onValueChanged.AddListener(text =>
            {
                SearchBarValueChanged?.Invoke(text);
                SetSearchBarClearButtonActive(!string.IsNullOrEmpty(text));
            });
            searchBar.inputField.onSubmit.AddListener(text => SearchBarSubmit?.Invoke(text));
            searchBar.clearSearchButton.onClick.AddListener(() => SearchBarClearButtonClicked?.Invoke());

            createCommunityButton.onClick.AddListener(() => CreateCommunityButtonClicked?.Invoke());

            invitesAndRequestsView.CommunityProfileOpened += OnInvitesAndRequestsCommunityProfileOpened;
            invitesAndRequestsView.RequestToJoinCommunityCanceled += OnCommunityRequestToJoinCanceled;
            invitesAndRequestsView.CommunityInvitationAccepted += OnCommunityInvitationAccepted;
            invitesAndRequestsView.CommunityInvitationRejected += OnCommunityInvitationRejected;
            invitesAndRequestsView.OpenProfilePassportRequested += OnOpenProfilePassport;
            invitesAndRequestsView.OpenUserChatRequested += OnOpenUserChat;
        }

        private void OnDestroy()
        {
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();

            createCommunityButton.onClick.RemoveAllListeners();

            invitesAndRequestsView.CommunityProfileOpened -= OnInvitesAndRequestsCommunityProfileOpened;
            invitesAndRequestsView.RequestToJoinCommunityCanceled -= OnCommunityRequestToJoinCanceled;
            invitesAndRequestsView.CommunityInvitationAccepted -= OnCommunityInvitationAccepted;
            invitesAndRequestsView.CommunityInvitationRejected -= OnCommunityInvitationRejected;
            invitesAndRequestsView.OpenProfilePassportRequested -= OnOpenProfilePassport;
            invitesAndRequestsView.OpenUserChatRequested -= OnOpenUserChat;
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void CleanSearchBar(bool raiseOnChangeEvent = true)
        {
            TMP_InputField.OnChangeEvent originalEvent = searchBar.inputField.onValueChanged;

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = new TMP_InputField.OnChangeEvent();

            searchBar.inputField.text = string.Empty;
            SetSearchBarClearButtonActive(false);

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = originalEvent;
        }

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);


        public void SetResultsSectionActive(bool isActive) =>
            rightSectionView.gameObject.SetActive(isActive);

        private void OnInvitesAndRequestsCommunityProfileOpened(string communityId) =>
            CommunityProfileOpened?.Invoke(communityId);

        private void OnCommunityRequestToJoinCanceled(string communityId, string requestId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityRequestToJoinCanceled?.Invoke(communityId, requestId);
        }

        private void OnCommunityInvitationAccepted(string communityId, string invitationId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityInvitationAccepted?.Invoke(communityId, invitationId);
        }

        private void OnCommunityInvitationRejected(string communityId, string invitationId, CommunityResultCardView cardView)
        {
            cardView.SetActionLoadingActive(true);
            CommunityInvitationRejected?.Invoke(communityId, invitationId);
        }

        private void OnOpenProfilePassport(ICommunityMemberData profile) =>
            OpenProfilePassportRequested?.Invoke(profile);

        private void OnOpenUserChat(ICommunityMemberData profile) =>
            OpenUserChatRequested?.Invoke(profile);

        public void SetThumbnailLoader(ThumbnailLoader newThumbnailLoader)
        {
            invitesAndRequestsView.SetThumbnailLoader(newThumbnailLoader);
        }
    }
}
