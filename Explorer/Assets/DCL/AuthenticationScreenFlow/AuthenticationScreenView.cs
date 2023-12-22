using MVC;
using UnityEngine;
using UnityEngine.Localization.Components;
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
        public Button CancelAuthenticationProcess { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ProgressContainer { get; private set; } = null!;

        [field: SerializeField]
        public Slider ProgressBar { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProgressLabel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject FinalizeContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [field: SerializeField]
        public Button UseAnotherAccountButton { get; private set; } = null!;
    }
}
