using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationBlocklistGuard
{
    public class BlockedScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button SupportButton { get; private set; } = null!;
    }
}
