using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;

namespace DCL.PrivateWorlds.UI
{
    /// <summary>
    /// Controller for the private world access popup.
    /// Supports two modes: PasswordRequired and AccessDenied. Only manages view lifecycle; result is set on inputData.
    /// Implements IBlocksChat so the MVC system automatically minimizes the chat when the popup opens,
    /// preventing the chat input field from fighting for focus with the password input.
    /// </summary>
    public class PrivateWorldPopupController : ControllerBase<PrivateWorldPopupView, PrivateWorldPopupParams>, IBlocksChat
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
