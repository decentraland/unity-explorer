using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    // TODO should be reused
    public class ChatTitlebarViewModel
    {
        public readonly IReactiveProperty<ProfileThumbnailViewModel> Thumbnail
            = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.ReadyToLoad());

        public TitlebarViewMode ViewMode;
        public string Username;
        public string Id;
        public string WalletId;
        public bool HasClaimedName;
        public Color ProfileColor { get; set; }

        public static ChatTitlebarViewModel CreateLoading(TitlebarViewMode viewMode)
        {
            return new ChatTitlebarViewModel
            {
                Username = "Loading...",
                ViewMode = viewMode,
                WalletId = string.Empty,
                HasClaimedName = false,
                ProfileColor = Color.gray,
            };
        }
    }

    public enum TitlebarViewMode { DirectMessage, Nearby, Community, MemberList }
}
