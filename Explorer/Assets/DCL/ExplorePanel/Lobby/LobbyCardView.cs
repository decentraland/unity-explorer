using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.Communities;
using System.Threading;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>A reusable destination or friend card. Actions are wired once when created.</summary>
    public sealed class LobbyCardView : MonoBehaviour
    {
        [SerializeField] private Sprite? defaultThumbnail;

        [SerializeField, FormerlySerializedAs("<Title>k__BackingField")] private TMP_Text? titleText;
        [SerializeField, FormerlySerializedAs("<Detail>k__BackingField")] private TMP_Text? detailText;
        [SerializeField, FormerlySerializedAs("<Status>k__BackingField")] private TMP_Text? statusText;
        [SerializeField, FormerlySerializedAs("<Thumbnail>k__BackingField")] private Image? thumbnail;
        [SerializeField, FormerlySerializedAs("<Surface>k__BackingField")] private Button? surface;
        [SerializeField, FormerlySerializedAs("<Primary>k__BackingField")] private Button? primary;
        [SerializeField, FormerlySerializedAs("<Chat>k__BackingField")] private Button? chat;
        [SerializeField, FormerlySerializedAs("<Profile>k__BackingField")] private Button? profile;

        private string imageUrl = string.Empty;
        private Action? selected;
        private Action? chatSelected;
        private Action? profileSelected;

        /// <summary>Identifies the bound item so a late update can verify the card still shows it.</summary>
        public string Key { get; set; } = string.Empty;

        public TMP_Text Title => titleText.EnsureNotNull();
        public TMP_Text Detail => detailText.EnsureNotNull();
        public TMP_Text Status => statusText.EnsureNotNull();
        public Image Thumbnail => thumbnail.EnsureNotNull();
        public Button Surface => surface.EnsureNotNull();
        public Button Primary => primary.EnsureNotNull();
        public Button Chat => chat.EnsureNotNull();
        public Button Profile => profile.EnsureNotNull();

        private void Awake()
        {
            Surface.onClick.AddListener(Select);
            Primary.onClick.AddListener(Select);
            Chat.onClick.AddListener(SelectChat);
            Profile.onClick.AddListener(SelectProfile);
        }

        public void Bind(string title, string detail, string status, string url, ThumbnailLoader thumbnails, CancellationToken ct,
            Action onSelected, Action? onChatSelected = null, Action? onProfileSelected = null)
        {
            Title.text = title;
            Detail.text = detail;
            Status.text = status;
            imageUrl = url;
            selected = onSelected;
            chatSelected = onChatSelected;
            profileSelected = onProfileSelected;
            Chat.gameObject.SetActive(onChatSelected != null);
            Profile.gameObject.SetActive(onProfileSelected != null);
            thumbnails.LoadCommunityThumbnailFromUrlAsync(url, Thumbnail.GetComponent<ImageView>(),
                defaultThumbnail, ct, false, () => imageUrl == url).Forget();
            gameObject.SetActive(true);
        }

        public void SetDetail(string detail) =>
            Detail.text = detail;

        private void Select() => selected?.Invoke();
        private void SelectChat() => chatSelected?.Invoke();
        private void SelectProfile() => profileSelected?.Invoke();
    }
}
