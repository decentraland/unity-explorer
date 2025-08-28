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
        public readonly bool IsOfficial;

        public readonly IReactiveProperty<ProfileThumbnailViewModel> ProfileThumbnail;

        public ChatMemberListViewModel(string userId, string walletId, string userName, bool isOnline, Color profileColor,
            bool hasClaimedName, bool isOfficial)
        {
            UserId = userId;
            WalletId = walletId;
            UserName = userName;
            IsOnline = isOnline;
            ProfileColor = profileColor;
            HasClaimedName = hasClaimedName;
            IsOfficial = isOfficial;

            ProfileThumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());
        }
    }
}
