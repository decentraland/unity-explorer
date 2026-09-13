using DCL.UI;
using DCL.Communities;
using System.Threading;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>A reusable destination or friend card. Actions are wired once when created.</summary>
    public sealed class LobbyCardView : MonoBehaviour
    {
        [SerializeField] private Sprite? defaultThumbnail;

        [field: SerializeField] public TMP_Text Title { get; private set; }
        [field: SerializeField] public TMP_Text Detail { get; private set; }
        [field: SerializeField] public TMP_Text Status { get; private set; }
        [field: SerializeField] public Image Thumbnail { get; private set; }
        [field: SerializeField] public Button Surface { get; private set; }
        [field: SerializeField] public Button Primary { get; private set; }
        [field: SerializeField] public Button Chat { get; private set; }
        [field: SerializeField] public Button Profile { get; private set; }

        private string imageUrl = string.Empty;
        private Action? selected;
        private Action? chatSelected;
        private Action? profileSelected;

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

        private void Select() => selected?.Invoke();
        private void SelectChat() => chatSelected?.Invoke();
        private void SelectProfile() => profileSelected?.Invoke();
    }
}
