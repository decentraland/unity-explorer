using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MemberData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string MUTUAL_FRIENDS_FORMAT = "{0} Mutual Friends";

        [field: SerializeField] private Image background { get; set; } = null!;
        [field: SerializeField] private Color normalColor { get; set; }
        [field: SerializeField] private Color hoveredColor { get; set; }
        [field: SerializeField] private Button mainButton { get; set; } = null!;
        [field: SerializeField] private Button contextMenuButton { get; set; } = null!;
        [field: SerializeField] private Button unbanButton { get; set; } = null!;

        [field: Header("User")]
        [field: SerializeField] private TMP_Text userName { get; set; } = null!;
        [field: SerializeField] private TMP_Text userNameTag { get; set; } = null!;
        [field: SerializeField] private GameObject verifiedIcon { get; set; } = null!;
        [field: SerializeField] private ProfilePictureView profilePicture { get; set; } = null!;
        [field: SerializeField] private TMP_Text mutualFriendsText { get; set; } = null!;
        [field: SerializeField] private TMP_Text roleText { get; set; } = null!;

        [field: Header("Friend buttons")]
        [field: SerializeField] private Button addFriendButton { get; set; } = null!;
        [field: SerializeField] private Button acceptFriendButton { get; set; } = null!;
        [field: SerializeField] private Button removeFriendButton { get; set; } = null!;
        [field: SerializeField] private Button cancelFriendButton { get; set; } = null!;
        [field: SerializeField] private Button unblockFriendButton { get; set; } = null!;

        [field: Header("Join request buttons")]
        [field: SerializeField] private Button deleteRequestButton { get; set; } = null!;
        [field: SerializeField] private Button acceptRequestButton { get; set; } = null!;
        [field: SerializeField] private Button cancelInviteButton { get; set; } = null!;

        private bool canUnHover = true;
        private bool isUserCard = false;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.MEMBERS;

        public MemberData? UserProfile { get; protected set; }

        public event Action<MemberData>? MainButtonClicked;
        public event Action<MemberData, Vector2, MemberListItemView>? ContextMenuButtonClicked;
        public event Action<MemberData>? FriendButtonClicked;
        public event Action<MemberData>? UnbanButtonClicked;
        public event Action<MemberData, InviteRequestIntention>? ManageRequestClicked;

        private void RemoveAllListeners()
        {
            MainButtonClicked = null;
            ContextMenuButtonClicked = null;
            FriendButtonClicked = null;
            UnbanButtonClicked = null;
            ManageRequestClicked = null;
        }

        internal bool CanUnHover
        {
            get => canUnHover;
            set
            {
                if (!canUnHover && value)
                {
                    canUnHover = value;
                    UnHover();
                }
                canUnHover = value;
            }
        }

        private void Awake()
        {
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile!));
            unbanButton.onClick.AddListener(() => UnbanButtonClicked?.Invoke(UserProfile!));
            contextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(UserProfile!, contextMenuButton.transform.position, this));

            addFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile!));
            acceptFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile!));
            removeFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile!));
            cancelFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile!));
            unblockFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile!));

            deleteRequestButton.onClick.AddListener(() => ManageRequestClicked?.Invoke(UserProfile!, InviteRequestIntention.reject));
            acceptRequestButton.onClick.AddListener(() => ManageRequestClicked?.Invoke(UserProfile!, InviteRequestIntention.accept));
            cancelInviteButton.onClick.AddListener(() => ManageRequestClicked?.Invoke(UserProfile!, InviteRequestIntention.cancel));

            background.color = normalColor;
        }

        public void Configure(MemberData memberProfile, MembersListView.MemberListSections section, bool isSelfCard, ProfileRepositoryWrapper profileDataProvider)
        {
            UnHover();
            UserProfile = memberProfile;

            Color userColor = memberProfile.GetUserNameColor();

            userName.text = memberProfile.name;
            userName.color = userColor;
            userNameTag.text = $"#{memberProfile.memberAddress[^4..]}";
            userNameTag.gameObject.SetActive(!memberProfile.hasClaimedName);
            verifiedIcon.SetActive(memberProfile.hasClaimedName);
            mutualFriendsText.text = string.Format(MUTUAL_FRIENDS_FORMAT, memberProfile.mutualFriends);
            mutualFriendsText.gameObject.SetActive(memberProfile.friendshipStatus != FriendshipStatus.friend && memberProfile.mutualFriends > 0);
            roleText.text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(memberProfile.role.ToString());
            roleText.transform.parent.gameObject.SetActive(memberProfile.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            profilePicture.Setup(profileDataProvider, memberProfile.GetUserNameColor(), memberProfile.profilePictureUrl);

            currentSection = section;
            isUserCard = isSelfCard;

            addFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.none && currentSection == MembersListView.MemberListSections.MEMBERS);
            acceptFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.request_received && currentSection == MembersListView.MemberListSections.MEMBERS);

            // Disable this button as part of the UI/UX decision to reduce the entry clutter, highlighting only non-friends. The old condition was:
            // !isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.friend && currentSection == MembersListView.MemberListSections.ALL
            removeFriendButton.gameObject.SetActive(false);

            cancelFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.request_sent && currentSection == MembersListView.MemberListSections.MEMBERS);
            unblockFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.blocked && currentSection == MembersListView.MemberListSections.MEMBERS);

            deleteRequestButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.REQUESTS);
            acceptRequestButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.REQUESTS);
            cancelInviteButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.INVITES);
            unbanButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.BANNED);
        }

        public void SubscribeToInteractions(Action<MemberData> mainButton,
            Action<MemberData, Vector2, MemberListItemView> contextMenuButton,
            Action<MemberData> friendButton,
            Action<MemberData> unbanButton,
            Action<MemberData, InviteRequestIntention> manageRequestClicked)
        {
            RemoveAllListeners();

            MainButtonClicked += mainButton;
            ContextMenuButtonClicked += contextMenuButton;
            FriendButtonClicked += friendButton;
            UnbanButtonClicked += unbanButton;
            ManageRequestClicked += manageRequestClicked;
        }

        private void UnHover()
        {
            contextMenuButton.gameObject.SetActive(false);
            background.color = normalColor;
        }

        private void Hover()
        {
            contextMenuButton.gameObject.SetActive(!isUserCard);
            background.color = hoveredColor;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canUnHover)
                UnHover();
        }
    }
}
