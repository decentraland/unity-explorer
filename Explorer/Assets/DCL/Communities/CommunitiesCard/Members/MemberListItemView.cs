using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string MUTUAL_FRIENDS_FORMAT = "{0} Mutual Friends";

        [field: SerializeField] private Image background { get; set; }
        [field: SerializeField] private Color normalColor { get; set; }
        [field: SerializeField] private Color hoveredColor { get; set; }
        [field: SerializeField] private Button mainButton { get; set; }
        [field: SerializeField] private Button contextMenuButton { get; set; }
        [field: SerializeField] private Button unbanButton { get; set; }

        [field: Header("User")]
        [field: SerializeField] private TMP_Text userName { get; set; }
        [field: SerializeField] private TMP_Text userNameTag { get; set; }
        [field: SerializeField] private GameObject verifiedIcon { get; set; }
        [field: SerializeField] private ProfilePictureView profilePicture { get; set; }
        [field: SerializeField] private TMP_Text mutualFriendsText { get; set; }
        [field: SerializeField] private TMP_Text roleText { get; set; }

        [field: Header("Friend buttons")]
        [field: SerializeField] private Button addFriendButton { get; set; }
        [field: SerializeField] private Button acceptFriendButton { get; set; }
        [field: SerializeField] private Button removeFriendButton { get; set; }
        [field: SerializeField] private Button cancelFriendButton { get; set; }
        [field: SerializeField] private Button unblockFriendButton { get; set; }

        private bool canUnHover = true;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.ALL;

        public MemberData UserProfile { get; protected set; }

        public event Action<MemberData>? MainButtonClicked;
        public event Action<MemberData, Vector2, MemberListItemView>? ContextMenuButtonClicked;
        public event Action<MemberData>? FriendButtonClicked;
        public event Action<MemberData>? UnbanButtonClicked;

        private void RemoveAllListeners()
        {
            MainButtonClicked = null;
            ContextMenuButtonClicked = null;
            FriendButtonClicked = null;
            UnbanButtonClicked = null;
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
            mainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
            unbanButton.onClick.AddListener(() => UnbanButtonClicked?.Invoke(UserProfile));
            contextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(UserProfile, contextMenuButton.transform.position, this));

            addFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            acceptFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            removeFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            cancelFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            unblockFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));

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
            profilePicture.Setup(profileDataProvider, memberProfile.GetUserNameColor(), memberProfile.profilePictureUrl, memberProfile.memberAddress);

            currentSection = section;

            addFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.none && currentSection == MembersListView.MemberListSections.ALL);
            acceptFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.request_received && currentSection == MembersListView.MemberListSections.ALL);
            removeFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.friend && currentSection == MembersListView.MemberListSections.ALL);
            cancelFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.request_sent && currentSection == MembersListView.MemberListSections.ALL);
            unblockFriendButton.gameObject.SetActive(!isSelfCard && memberProfile.friendshipStatus == FriendshipStatus.blocked && currentSection == MembersListView.MemberListSections.ALL);
        }

        public void SubscribeToInteractions(Action<MemberData> mainButton,
            Action<MemberData, Vector2, MemberListItemView> contextMenuButton,
            Action<MemberData> friendButton,
            Action<MemberData> unbanButton)
        {
            RemoveAllListeners();

            MainButtonClicked += mainButton;
            ContextMenuButtonClicked += contextMenuButton;
            FriendButtonClicked += friendButton;
            UnbanButtonClicked += unbanButton;
        }

        private void UnHover()
        {
            contextMenuButton.gameObject.SetActive(false);
            unbanButton.gameObject.SetActive(false);
            background.color = normalColor;
        }

        private void Hover()
        {
            contextMenuButton.gameObject.SetActive(true);
            unbanButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.BANNED);
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
