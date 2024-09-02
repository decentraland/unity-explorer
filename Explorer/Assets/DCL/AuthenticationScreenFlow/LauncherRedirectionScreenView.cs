using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class LauncherRedirectionScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseWithLauncherButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;
    }
}
