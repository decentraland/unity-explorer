using DCL.CharacterPreview;
using DCL.Communities;
using DCL.UI;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Authored Home layout, including the avatar stage and discovery rails.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [field: SerializeField] public CharacterPreviewView Avatar { get; private set; }
        [SerializeField] private Button customizeAvatar;
        [field: SerializeField] public TMP_Text PlayerName { get; private set; }
        [field: SerializeField] public Image ProfilePortrait { get; private set; }
        [SerializeField] private Button jumpIn;
        [SerializeField] private Button destination;
        [field: SerializeField] public TMP_Text DestinationStatus { get; private set; }
        [field: SerializeField] public Image Background { get; private set; }
        [SerializeField] private Button browseEvents;
        [SerializeField] private Button browseFriends;
        [SerializeField] private Button browseReturn;
        [SerializeField] private Button previousFeatured;
        [SerializeField] private Button nextFeatured;
        [SerializeField] private Button search;
        [SerializeField] private Button notifications;
        [field: SerializeField] public GameObject FriendsSection { get; private set; }
        [field: SerializeField] public TMP_Text ReturnStatus { get; private set; }
        [field: SerializeField] public TMP_Text FeaturedStatus { get; private set; }
        [field: SerializeField] public GameObject LiveSection { get; private set; }
        [field: SerializeField] public RectTransform FriendsContent { get; private set; }
        [field: SerializeField] public RectTransform EventsContent { get; private set; }
        [field: SerializeField] public RectTransform ReturnContent { get; private set; }
        [field: SerializeField] public RectTransform FeaturedContent { get; private set; }
        [field: SerializeField] public LobbyCardView PlaceCard { get; private set; }
        [field: SerializeField] public LobbyCardView ReturnCard { get; private set; }
        [field: SerializeField] public LobbyCardView FriendCard { get; private set; }
        [field: SerializeField] public LobbyCardView EventCard { get; private set; }
        private string portraitUrl = string.Empty;

        public event Action? CustomizeAvatarClicked;
        public event Action? JumpInClicked;
        public event Action? DestinationClicked;
        public event Action? BrowseEventsClicked;
        public event Action? BrowseFriendsClicked;
        public event Action? BrowseReturnClicked;
        public event Action? PreviousFeaturedClicked;
        public event Action? NextFeaturedClicked;
        public event Action? SearchClicked;
        public event Action<RectTransform>? NotificationsClicked;

        public void SetProfilePortrait(string url, ThumbnailLoader thumbnails, CancellationToken ct)
        {
            portraitUrl = url;
            thumbnails.LoadCommunityThumbnailFromUrlAsync(url, ProfilePortrait.GetComponent<ImageView>(),
                ProfilePortrait.sprite, ct, false, () => portraitUrl == url).Forget();
        }

        private void Awake()
        {
            customizeAvatar.onClick.AddListener(() => CustomizeAvatarClicked?.Invoke());
            jumpIn.onClick.AddListener(() => JumpInClicked?.Invoke());
            destination.onClick.AddListener(() => DestinationClicked?.Invoke());
            browseEvents.onClick.AddListener(() => BrowseEventsClicked?.Invoke());
            browseFriends.onClick.AddListener(() => BrowseFriendsClicked?.Invoke());
            browseReturn.onClick.AddListener(() => BrowseReturnClicked?.Invoke());
            previousFeatured.onClick.AddListener(() => PreviousFeaturedClicked?.Invoke());
            nextFeatured.onClick.AddListener(() => NextFeaturedClicked?.Invoke());
            search.onClick.AddListener(() => SearchClicked?.Invoke());
            notifications.onClick.AddListener(OnNotificationsClicked);
        }

        private void OnNotificationsClicked() =>
            NotificationsClicked?.Invoke((RectTransform)notifications.transform);
    }
}
