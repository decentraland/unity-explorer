using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Button LoginButton { get; private set; } = null!;

        [field: SerializeField]
        public GameObject PendingAuthentication { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelAuthenticationProcess { get; private set; }
    }
}
