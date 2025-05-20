using DCL.UI.ProfileElements;
using MVC;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListSingleItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IViewWithGlobalDependencies
    {
        private const string MUTUAL_FRIENDS_FORMAT = "{0} Mutual Friends";

        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }
        [field: SerializeField] public Button ContextMenuButton { get; private set; }

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
        public GetCommunityMembersResponse.MemberData UserProfile { get; protected set; }

        public event Action<GetCommunityMembersResponse.MemberData>? MainButtonClicked;
        public event Action<GetCommunityMembersResponse.MemberData, Vector2, MemberListSingleItemView>? ContextMenuButtonClicked;
        public event Action<GetCommunityMembersResponse.MemberData>? FriendButtonClicked;

        public void RemoveAllListeners()
        {
            MainButtonClicked = null;
            ContextMenuButtonClicked = null;
            FriendButtonClicked = null;
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
            ContextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(UserProfile, ContextMenuButton.transform.position, this));

            AddFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            AcceptFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            RemoveFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            CancelFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
            UnblockFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile));
        }

        private void Start()
        {
            Background.color = NormalColor;
        }

        public void Configure(GetCommunityMembersResponse.MemberData memberProfile)
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

            AddFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.none);
            AcceptFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.request_received);
            RemoveFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.friend);
            CancelFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.request_sent);
            UnblockFriendButton.gameObject.SetActive(memberProfile.friendshipStatus == FriendshipStatus.blocked);
        }

        private void UnHover()
        {
            ContextMenuButton.gameObject.SetActive(false);
            Background.color = NormalColor;
        }

        private void Hover()
        {
            ContextMenuButton.gameObject.SetActive(true);
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
