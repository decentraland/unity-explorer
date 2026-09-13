using DCL.CharacterPreview;
using DCL.Communities;
using DCL.UI;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Authored Home layout, including the avatar stage and discovery rails.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("<Avatar>k__BackingField")] private CharacterPreviewView? avatar;
        [SerializeField] private Button? customizeAvatar;
        [SerializeField, FormerlySerializedAs("<PlayerName>k__BackingField")] private TMP_Text? playerName;
        [SerializeField, FormerlySerializedAs("<ProfilePortrait>k__BackingField")] private Image? profilePortrait;
        [SerializeField] private Button? jumpIn;
        [SerializeField] private Button? destination;
        [SerializeField, FormerlySerializedAs("<DestinationStatus>k__BackingField")] private TMP_Text? destinationStatus;
        [SerializeField, FormerlySerializedAs("<Background>k__BackingField")] private Image? background;
        [SerializeField] private Button? browseEvents;
        [SerializeField] private Button? browseFriends;
        [SerializeField] private Button? browseReturn;
        [SerializeField] private Button? previousFeatured;
        [SerializeField] private Button? nextFeatured;
        [SerializeField] private Button? search;
        [SerializeField] private Button? notifications;
        [SerializeField, FormerlySerializedAs("<FriendsSection>k__BackingField")] private GameObject? friendsSection;
        [SerializeField, FormerlySerializedAs("<ReturnStatus>k__BackingField")] private TMP_Text? returnStatus;
        [SerializeField, FormerlySerializedAs("<FeaturedStatus>k__BackingField")] private TMP_Text? featuredStatus;
        [SerializeField, FormerlySerializedAs("<LiveSection>k__BackingField")] private GameObject? liveSection;
        [SerializeField, FormerlySerializedAs("<FriendsContent>k__BackingField")] private RectTransform? friendsContent;
        [SerializeField, FormerlySerializedAs("<EventsContent>k__BackingField")] private RectTransform? eventsContent;
        [SerializeField, FormerlySerializedAs("<ReturnContent>k__BackingField")] private RectTransform? returnContent;
        [SerializeField, FormerlySerializedAs("<FeaturedContent>k__BackingField")] private RectTransform? featuredContent;
        [SerializeField, FormerlySerializedAs("<PlaceCard>k__BackingField")] private LobbyCardView? placeCard;
        [SerializeField, FormerlySerializedAs("<ReturnCard>k__BackingField")] private LobbyCardView? returnCard;
        [SerializeField, FormerlySerializedAs("<FriendCard>k__BackingField")] private LobbyCardView? friendCard;
        [SerializeField, FormerlySerializedAs("<EventCard>k__BackingField")] private LobbyCardView? eventCard;
        private string portraitUrl = string.Empty;

        public CharacterPreviewView Avatar => avatar.EnsureNotNull();
        public TMP_Text PlayerName => playerName.EnsureNotNull();
        public Image ProfilePortrait => profilePortrait.EnsureNotNull();
        public TMP_Text DestinationStatus => destinationStatus.EnsureNotNull();
        public Image Background => background.EnsureNotNull();
        public GameObject FriendsSection => friendsSection.EnsureNotNull();
        public TMP_Text ReturnStatus => returnStatus.EnsureNotNull();
        public TMP_Text FeaturedStatus => featuredStatus.EnsureNotNull();
        public GameObject LiveSection => liveSection.EnsureNotNull();
        public RectTransform FriendsContent => friendsContent.EnsureNotNull();
        public RectTransform EventsContent => eventsContent.EnsureNotNull();
        public RectTransform ReturnContent => returnContent.EnsureNotNull();
        public RectTransform FeaturedContent => featuredContent.EnsureNotNull();
        public LobbyCardView PlaceCard => placeCard.EnsureNotNull();
        public LobbyCardView ReturnCard => returnCard.EnsureNotNull();
        public LobbyCardView FriendCard => friendCard.EnsureNotNull();
        public LobbyCardView EventCard => eventCard.EnsureNotNull();

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
            customizeAvatar.EnsureNotNull().onClick.AddListener(() => CustomizeAvatarClicked?.Invoke());
            jumpIn.EnsureNotNull().onClick.AddListener(() => JumpInClicked?.Invoke());
            destination.EnsureNotNull().onClick.AddListener(() => DestinationClicked?.Invoke());
            browseEvents.EnsureNotNull().onClick.AddListener(() => BrowseEventsClicked?.Invoke());
            browseFriends.EnsureNotNull().onClick.AddListener(() => BrowseFriendsClicked?.Invoke());
            browseReturn.EnsureNotNull().onClick.AddListener(() => BrowseReturnClicked?.Invoke());
            previousFeatured.EnsureNotNull().onClick.AddListener(() => PreviousFeaturedClicked?.Invoke());
            nextFeatured.EnsureNotNull().onClick.AddListener(() => NextFeaturedClicked?.Invoke());
            search.EnsureNotNull().onClick.AddListener(() => SearchClicked?.Invoke());
            notifications.EnsureNotNull().onClick.AddListener(OnNotificationsClicked);
        }

        private void OnNotificationsClicked() =>
            NotificationsClicked?.Invoke((RectTransform)notifications.EnsureNotNull().transform);
    }
}
