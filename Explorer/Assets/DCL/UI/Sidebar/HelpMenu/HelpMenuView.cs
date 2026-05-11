using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar.HelpMenu
{
    public class HelpMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseAreaButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField] public Button MouseAndKeyControlsButton { get; private set; } = null!;
        [field: SerializeField] public Button FaqButton { get; private set; } = null!;
        [field: SerializeField] public Button ContactSupportButton { get; private set; } = null!;
        [field: SerializeField] public Button DiscordButton { get; private set; } = null!;
    }
}
