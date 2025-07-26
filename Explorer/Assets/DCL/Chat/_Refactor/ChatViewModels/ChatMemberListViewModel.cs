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

        public bool IsLoading;
        public Sprite? ProfilePicture;

        public ChatMemberListViewModel(string userId, string walletId, string userName, bool isOnline, Color profileColor,
            bool hasClaimedName)
        {
            UserId = userId;
            WalletId = walletId;
            UserName = userName;
            IsOnline = isOnline;
            ProfileColor = profileColor;
            HasClaimedName = hasClaimedName;

            ProfilePicture = null;
            IsLoading = true;
        }
    }
}
