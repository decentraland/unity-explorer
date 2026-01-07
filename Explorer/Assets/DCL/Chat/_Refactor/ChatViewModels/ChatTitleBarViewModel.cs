using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatTitlebarViewModel
    {
        public readonly IReactiveProperty<ProfileThumbnailViewModel> Thumbnail;

        public TitlebarViewMode ViewMode { get; set; }
        public string Username { get; internal set; }
        public string Id { get; }
        public bool IsOnline { get; set; }
        public string WalletId { get; }
        public bool HasClaimedName { get; }
        public bool IsOfficial { get; set; }

        public Color Color => Thumbnail.Value.ProfileColor;

        public ChatTitlebarViewModel(string id, string username, string walletId)
        {
            Username = username;
            Id = id;
            WalletId = walletId;
            Thumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.ReadyToLoad());
        }

        public ChatTitlebarViewModel(string username) : this(string.Empty, username, string.Empty) { }

        public ChatTitlebarViewModel(Profile.CompactInfo profile)
        {
            Id = profile.UserId;
            WalletId = profile.WalletId!;
            HasClaimedName = profile.HasClaimedName;
            Thumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.ReadyToLoad(profile.UserNameColor));
            Username = profile.DisplayName;
        }

        public static ChatTitlebarViewModel CreateLoading(TitlebarViewMode viewMode)
        {
            var viewModel = new ChatTitlebarViewModel("Loading...")
            {
                ViewMode = viewMode,
            };

            viewModel.Thumbnail.SetColor(Color.gray);
            return viewModel;
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
