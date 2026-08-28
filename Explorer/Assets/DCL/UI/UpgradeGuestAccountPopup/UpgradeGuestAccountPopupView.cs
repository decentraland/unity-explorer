using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.UpgradeGuestAccountPopup
{
    public class UpgradeGuestAccountPopupView : ViewBase, IView
    {
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button UpgradeAccountButton { get; private set; } = null!;
    }
}
