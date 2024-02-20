using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class SystemMenuView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public Button LogoutButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ExitAppButton { get; private set; } = null!;

        [field: SerializeField]
        public Button PrivacyPolicyButton { get; private set; } = null!;

        [field: SerializeField]
        public Button TermsOfServiceButton { get; private set; } = null!;
    }
}
