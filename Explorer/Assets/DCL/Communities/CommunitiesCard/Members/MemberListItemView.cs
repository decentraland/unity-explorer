using DCL.UI.ProfileElements;
using MVC;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IViewWithGlobalDependencies
    {
        private const string MUTUAL_FRIENDS_FORMAT = "{0} Mutual Friends";

        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Button UnbanButton { get; private set; }

        [field: Header("User")]
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public GameObject VerifiedIcon { get; private set; }
        [field: SerializeField] public ProfilePictureView ProfilePicture { get; private set; }
        [field: SerializeField] public TMP_Text MutualFriendsText { get; private set; }
        [field: SerializeField] public TMP_Text RoleText { get; private set; }

        [field: Header("Friend buttons")]
        [field: SerializeField] public Button AddFriendButton { get; private set; }
        [field: SerializeField] public Button AcceptFriendButton { get; private set; }
        [field: SerializeField] public Button RemoveFriendButton { get; private set; }
        [field: SerializeField] public Button CancelFriendButton { get; private set; }
        [field: SerializeField] public Button UnblockFriendButton { get; private set; }

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
            MainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
            UnbanButton.onClick.AddListener(() => UnbanButtonClicked?.Invoke(UserProfile));
            ContextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(UserProfile, ContextMenuButton.transform.position, this));

            AddFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            AcceptFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            RemoveFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            CancelFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            UnblockFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));

            Background.color = NormalColor;
        }

        public void Configure(MemberData memberProfile, MembersListView.MemberListSections section)
        {
            UnHover();
            UserProfile = memberProfile;

            Color userColor = memberProfile.UserNameColor;

            UserName.text = memberProfile.name;
            UserName.color = userColor;
            UserNameTag.text = $"#{memberProfile.id[^4..]}";
            UserNameTag.gameObject.SetActive(!memberProfile.hasClaimedName);
            VerifiedIcon.SetActive(memberProfile.hasClaimedName);
            MutualFriendsText.text = string.Format(MUTUAL_FRIENDS_FORMAT, memberProfile.mutualFriends);
            MutualFriendsText.gameObject.SetActive(memberProfile.friendshipStatus != FriendshipStatus.friend);
            RoleText.text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(memberProfile.role.ToString());
            RoleText.transform.parent.gameObject.SetActive(memberProfile.role is CommunityMemberRole.owner or CommunityMemberRole.moderator);
            ProfilePicture.Setup(memberProfile.UserNameColor, memberProfile.profilePicture, memberProfile.id);

            currentSection = section;

            AddFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.none && currentSection == MembersListView.MemberListSections.ALL);
            AcceptFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.request_received && currentSection == MembersListView.MemberListSections.ALL);
            RemoveFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.friend && currentSection == MembersListView.MemberListSections.ALL);
            CancelFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.request_sent && currentSection == MembersListView.MemberListSections.ALL);
            UnblockFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.blocked && currentSection == MembersListView.MemberListSections.ALL);
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
            ContextMenuButton.gameObject.SetActive(false);
            UnbanButton.gameObject.SetActive(false);
            Background.color = NormalColor;
        }

        private void Hover()
        {
            ContextMenuButton.gameObject.SetActive(true);
            UnbanButton.gameObject.SetActive(currentSection == MembersListView.MemberListSections.BANNED);
            Background.color = HoveredColor;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canUnHover)
                UnHover();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            ProfilePicture.InjectDependencies(dependencies);
        }
    }
}
