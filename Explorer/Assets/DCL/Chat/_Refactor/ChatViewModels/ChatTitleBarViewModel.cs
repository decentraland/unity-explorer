using DCL.UI.ProfileElements;
using DCL.Utilities;
using DCL.Web3;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    // TODO should be reused
    public class ChatTitlebarViewModel
    {
        public readonly IReactiveProperty<ProfileThumbnailViewModel> Thumbnail
            = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());

        public Mode ViewMode;
        public string Username;
        public string Id;
        public string WalletId;
        public bool HasClaimedName;
        public Color ProfileColor { get; set; }

        public static ChatTitlebarViewModel CreateLoading(Mode viewMode) =>
            new ()
            {
                Username = "Loading...",
                ViewMode = viewMode,
                WalletId = string.Empty,
                HasClaimedName = false,
                ProfileColor = Color.gray,
            };
    }

    public enum Mode { DirectMessage, Nearby, MemberList }
}
