using DCL.Web3;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatTitlebarViewModel
    {
        public TitlebarViewMode ViewMode;
        public string Username;
        public Color UsernameColor;
        public string Id;
        public string WalletId;
        public Sprite? ProfileSprite;
        public bool HasClaimedName;
        public bool IsLoadingProfile { get; set; }
        public Color ProfileColor { get; set; }

        public static ChatTitlebarViewModel CreateLoading(TitlebarViewMode viewMode)
        {
            return new ChatTitlebarViewModel
            {
                Username = "Loading...",
                IsLoadingProfile = true,
                ViewMode = viewMode,
                WalletId = string.Empty,
                HasClaimedName = false,
                ProfileSprite = null,
                ProfileColor = Color.gray
            };
        }
    }

    public enum TitlebarViewMode { DirectMessage, Nearby, Community, MemberList }
}