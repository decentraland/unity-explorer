using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMemberListViewModel
    {
        public readonly string UserId;
        public readonly string WalletId;
        public readonly string UserName;
        public readonly bool IsOnline;
        public readonly Color ProfileColor;
        public readonly bool HasClaimedName;

        public readonly IReactiveProperty<ProfileThumbnailViewModel> ProfileThumbnail;

        public ChatMemberListViewModel(string userId, string walletId, string userName, bool isOnline, Color profileColor,
            bool hasClaimedName)
        {
            UserId = userId;
            WalletId = walletId;
            UserName = userName;
            IsOnline = isOnline;
            ProfileColor = profileColor;
            HasClaimedName = hasClaimedName;

            ProfileThumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());
        }
    }
}
