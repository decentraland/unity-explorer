using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    public class GuestOrSignUpAuthView : ViewBase
    {
        [field: SerializeField] public Button PlayAsGuestButton { get; private set; } = null!;
        [field: SerializeField] public Button LoginOrSignupButton { get; private set; } = null!;

        public void Show() =>
            ShowAsync(CancellationToken.None).Forget();

        public void Hide() =>
            HideAsync(CancellationToken.None).Forget();
    }
}