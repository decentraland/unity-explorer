using MVC;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatView : ViewBase, IView
    {
        [field: SerializeField]
        public Transform MessagesContainer { get; private set; }
    }
}
