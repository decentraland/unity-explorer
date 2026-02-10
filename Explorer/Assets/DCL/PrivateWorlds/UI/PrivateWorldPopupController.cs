using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using DCL.Input;
using DCL.Input.Component;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// Controller for the private world access popup.
    /// Supports two modes: PasswordRequired and AccessDenied. Only manages view lifecycle; result is set on inputData.
    /// </summary>
    public class PrivateWorldPopupController : ControllerBase<PrivateWorldPopupView, PrivateWorldPopupParams>
    {
        private readonly IInputBlock inputBlock;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PrivateWorldPopupController(ViewFactoryMethod viewFactory, IInputBlock inputBlock)
            : base(viewFactory)
        {
            this.inputBlock = inputBlock;
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.Configure(inputData);
        }

        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            if (inputData.Mode == PrivateWorldPopupMode.PasswordRequired)
                viewInstance!.FocusPasswordInput();
        }

        protected override void OnViewClose()
        {
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
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
