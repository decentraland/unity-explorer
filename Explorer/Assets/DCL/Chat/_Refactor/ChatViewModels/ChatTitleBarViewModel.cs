using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatTitlebarViewModel
    {
        public readonly IReactiveProperty<ProfileThumbnailViewModel> Thumbnail
            = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.ReadyToLoad());

        public TitlebarViewMode ViewMode { get; set; }
        public string Username { get; set; }
        public string Id { get; set; }
        public bool IsOnline { get; set; }
        public string WalletId { get; set; }
        public bool HasClaimedName { get; set; }
        public Color ProfileColor { get; set; }

        public static ChatTitlebarViewModel CreateLoading(TitlebarViewMode viewMode)
        {
            return new ChatTitlebarViewModel
            {
                Username = "Loading...", ViewMode = viewMode, WalletId = string.Empty, HasClaimedName = false,
                IsOnline = false, ProfileColor = Color.gray
            };
        }

        /// <summary>
        ///     NOTE: instead of using this method we should follow the pattern like here in
        ///     NOTE: ChatMessageViewModel
        /// </summary>
        /// <param name="thumbnail"></param>
        public void SetThumbnail(ProfileThumbnailViewModel thumbnail)
        {
            (Thumbnail as ReactiveProperty<ProfileThumbnailViewModel>)!.Value = thumbnail;
        }
    }

    public enum TitlebarViewMode { DirectMessage, Nearby, Community, MemberList }
}
