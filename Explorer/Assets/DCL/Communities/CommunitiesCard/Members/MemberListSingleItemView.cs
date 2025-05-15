using DCL.Friends;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using MVC;
using System;
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

        [field: Header("Friend buttons")]
        [field: SerializeField] public Button AddFriendButton { get; private set; }
        [field: SerializeField] public Button AcceptFriendButton { get; private set; }
        [field: SerializeField] public Button RemoveFriendButton { get; private set; }
        [field: SerializeField] public Button CancelFriendButton { get; private set; }
        [field: SerializeField] public Button UnblockFriendButton { get; private set; }

        private bool canUnHover = true;
        public Profile UserProfile { get; protected set; }

        public event Action<Profile>? MainButtonClicked;
        public event Action<Profile>? ContextMenuButtonClicked;
        public event Action<Profile, FriendshipStatus>? FriendButtonClicked;

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
            ContextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(UserProfile));

            // AddFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile, friendShipStatus));
            // AcceptFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile, friendShipStatus));
            // RemoveFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile, friendShipStatus));
            // CancelFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile, friendShipStatus));
            // UnblockFriendButton.onClick.AddListener(() => FriendButtonClicked?.Invoke(UserProfile, friendShipStatus));
        }

        private void Start()
        {
            Background.color = NormalColor;
        }

        public void Configure(Profile memberProfile)
        {
            UnHover();
            UserProfile = memberProfile;

            Color userColor = memberProfile.UserNameColor;

            UserName.text = memberProfile.Name;
            UserName.color = userColor;
            UserNameTag.text = $"#{memberProfile.UserId[^4..]}";
            UserNameTag.gameObject.SetActive(!memberProfile.HasClaimedName);
            VerifiedIcon.SetActive(memberProfile.HasClaimedName);
            MutualFriendsText.text = string.Format(MUTUAL_FRIENDS_FORMAT, 32);
            ProfilePicture.Setup(memberProfile.UserNameColor, memberProfile.Avatar.FaceSnapshotUrl.ToString(), memberProfile.UserId);

            //TODO (Lorenzo): the friendship status should be passed from the controller
            // AddFriendButton.gameObject.SetActive(friendShipStatus == FriendshipStatus.NONE);
            // AcceptFriendButton.gameObject.SetActive(friendShipStatus == FriendshipStatus.REQUEST_RECEIVED);
            // RemoveFriendButton.gameObject.SetActive(friendShipStatus == FriendshipStatus.FRIEND);
            // CancelFriendButton.gameObject.SetActive(friendShipStatus == FriendshipStatus.REQUEST_SENT);
            // UnblockFriendButton.gameObject.SetActive(friendShipStatus == FriendshipStatus.BLOCKED);
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
