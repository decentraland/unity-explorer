using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    public class LobbyView : ViewBase, IView
    {
        [field: SerializeField]
        public Button JumpInButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;
    }
}
