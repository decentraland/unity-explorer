using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using DCL.Utility;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// Controller for the private world access popup.
    /// Supports two modes: PasswordRequired and AccessDenied. Only manages view lifecycle; result is set on inputData.
    /// </summary>
    public class PrivateWorldPopupController : ControllerBase<PrivateWorldPopupView, PrivateWorldPopupParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PrivateWorldPopupController(ViewFactoryMethod viewFactory)
            : base(viewFactory) { }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.Configure(inputData);
        }

        protected override void OnViewShow()
        {
            if (inputData.Mode == PrivateWorldPopupMode.PasswordRequired)
                viewInstance!.FocusPasswordInput();
        }

        protected override void OnViewClose()
        {
            viewInstance?.ResetState();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            int index = await UniTask.WhenAny(
                viewInstance!.PasswordConfirmButton.OnClickAsync(ct),
                viewInstance.PasswordCancelButton.OnClickAsync(ct),
                viewInstance.InvitationConfirmButton.OnClickAsync(ct),
                viewInstance.BackgroundCloseButton.OnClickAsync(ct)
            );

            if (index == 0)
            {
                if (inputData.Mode == PrivateWorldPopupMode.PasswordRequired)
                {
                    inputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                    inputData.EnteredPassword = viewInstance.EnteredPassword;
                }
                else
                {
                    inputData.Result = PrivateWorldPopupResult.Cancelled;
                    inputData.EnteredPassword = null;
                }
            }
            else
            {
                inputData.Result = PrivateWorldPopupResult.Cancelled;
                inputData.EnteredPassword = null;
            }
        }
    }
}
