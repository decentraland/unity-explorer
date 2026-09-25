using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Compact friend card of the lobby rail: picture with the online dot, name, and a location row that hovering swaps for a
    ///     Join button while the friend can be joined. Pooled by the rail, so its callbacks are wired once and rebound by content.
    /// </summary>
    public class LobbyFriendCardView : MonoBehaviour
    {
        private const string LOCATING_TEXT = "Locating…";

        public Action<LobbyFriendCardView>? Clicked;
        public Action<LobbyFriendCardView>? JoinClicked;

        [SerializeField] private OnlineStatusConfiguration onlineStatusConfiguration = null!;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());

        private CancellationTokenSource? thumbnailCts;
        private bool hovered;
        private bool joinAvailable;

        [field: SerializeField]
        public Button Button { get; private set; } = null!;

        [field: SerializeField]
        public HoverableUiElement Hover { get; private set; } = null!;

        [field: SerializeField]
        public ProfilePictureView ProfilePicture { get; private set; } = null!;

        [field: SerializeField]
        public Image OnlineIndicator { get; private set; } = null!;

        [field: SerializeField]
        public SimpleUserNameElement UserName { get; private set; } = null!;

        [field: SerializeField]
        public GameObject LocationGroup { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text LocationText { get; private set; } = null!;

        [field: SerializeField]
        public Button JoinButton { get; private set; } = null!;

        /// <summary>
        ///     Wallet of the friend currently shown; a late asynchronous result checks it before touching the card.
        /// </summary>
        public string UserId { get; private set; } = string.Empty;

        public void Bind(in Profile.CompactInfo profile, OnlineStatus status, CancellationToken ct)
        {
            UserName.Setup(profile);
            OnlineIndicator.color = onlineStatusConfiguration.GetConfiguration(status).StatusColor;

            // Rebinding the same friend keeps the picture: refreshes happen on every status change in the list
            if (string.Equals(UserId, profile.UserId.Value, StringComparison.OrdinalIgnoreCase)) return;

            UserId = profile.UserId.Value;
            thumbnailCts = thumbnailCts.SafeRestartLinked(ct);
            thumbnail.SetLoading(profile.UserNameColor);
            GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, thumbnailCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
        }

        public void SetLocating() =>
            SetLocation(LOCATING_TEXT, canJoin: false);

        public void SetLocation(string label, bool canJoin)
        {
            LocationText.text = label;
            joinAvailable = canJoin;
            UpdateJoinVisibility();
        }

        private void Awake()
        {
            ProfilePicture.Bind(thumbnail);
            Button.onClick.AddListener(OnClicked);
            JoinButton.onClick.AddListener(OnJoinClicked);
            Hover.HoverStateChanged += OnHoverChanged;
        }

        private void OnDisable()
        {
            hovered = false;
            UpdateJoinVisibility();
        }

        private void OnDestroy()
        {
            thumbnailCts.SafeCancelAndDispose();
        }

        private void OnClicked() =>
            Clicked?.Invoke(this);

        private void OnJoinClicked() =>
            JoinClicked?.Invoke(this);

        private void OnHoverChanged(bool isHovered)
        {
            hovered = isHovered;
            UpdateJoinVisibility();
        }

        private void UpdateJoinVisibility()
        {
            bool showJoin = hovered && joinAvailable;
            JoinButton.gameObject.SetActive(showJoin);
            LocationGroup.SetActive(!showJoin);
        }
    }
}
