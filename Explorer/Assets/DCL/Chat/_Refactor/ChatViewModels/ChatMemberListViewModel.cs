using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMemberListViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public Sprite ProfilePicture { get; set; }
        public bool IsOnline { get; set; }
        public Color ProfileColor { get; set; }

        public bool IsLoading { get; set; }
    }
}