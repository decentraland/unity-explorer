using DCL.Web3;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatTitlebarViewModel
    {
        public Mode ViewMode;
        public string Name;
        public Color UsernameColor;
        public string WalletId;
        public Sprite ProfileSprite;
        public bool HasClaimedName;
        public bool IsLoadingProfile { get; set; }
        public Color ProfileColor { get; set; }
        
        public static ChatTitlebarViewModel CreateLoading(Mode viewMode)
        {
            return new ChatTitlebarViewModel
            {
                Name = "Loading...",
                IsLoadingProfile = true,
                ViewMode = viewMode,
                WalletId = string.Empty,
                HasClaimedName = false,
                ProfileSprite = null,
                ProfileColor = Color.gray
            };
        }
    }

    public enum Mode { DirectMessage, Nearby, MemberList }
}