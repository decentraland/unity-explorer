using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class MinimumSpecsScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Button ExitButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ContinueButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ReadMoreButton { get; private set; } = null!;

        [field: SerializeField]
        public Toggle DontShowAgainToggle { get; private set; } = null!;
    }
}
