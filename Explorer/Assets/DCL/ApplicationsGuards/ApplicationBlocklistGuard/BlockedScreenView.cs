using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationBlocklistGuard
{
    public class BlockedScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public TMP_Text InfoText { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button SupportButton { get; private set; } = null!;
    }
}
