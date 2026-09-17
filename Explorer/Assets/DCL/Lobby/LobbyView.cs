using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    public class LobbyView : ViewBase, IView
    {
        [field: SerializeField]
        public Button JumpInButton { get; private set; } = null!;

        [field: Space(10)]
        [field: Header("Top Bar")]
        [field: SerializeField]
        public CreditsPanelView CreditsPanelView { get; private set; } = null!;

        [field: SerializeField]
        public ProfileWidgetView ProfileWidgetView { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;
    }
}
