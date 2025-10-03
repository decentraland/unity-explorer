using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ApplicationsGuards.ApplicationLoadErrorGuard
{
    public class ApplicationLoadErrorGuardView : ViewBase, IView
    {
        [field: SerializeField]
        public Button ExitButton { get; private set; }

        [field: SerializeField]
        public Button RestartButton { get; private set; }
    }
}
