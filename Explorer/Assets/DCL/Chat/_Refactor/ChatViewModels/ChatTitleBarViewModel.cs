using DCL.Web3;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatTitlebarViewModel
    {
        public Mode ViewMode;
        public string Title;
        public Web3Address UserProfileId;
        public Sprite ProfileSprite;
        public bool HasClaimedName;
        public bool IsLoadingProfile { get; set; }
        public Color ProfileColor { get; set; }
    }

    public enum Mode { DirectMessage, Nearby, MemberList }
}