using MVC;
using TMPro;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatView : ViewBase, IView
    {
        [field: SerializeField]
        public Transform MessagesContainer { get; private set; }

        [field: SerializeField]
        public TMP_InputField InputField { get; private set; }

        [field: SerializeField]
        public CharacterCounterView CharacterCounter { get; private set; }
    }
}
