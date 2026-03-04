using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.PrivateWorlds;
using DCL.UI.SharedSpaceManager;
using MVC;
using System;
using System.Threading;
using Utility;

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
        private readonly IWorldPermissionsService worldPermissionsService;

        private UniTaskCompletionSource closeTaskCompletionSource = new ();
        private CancellationTokenSource? validateCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public PrivateWorldPopupController(ViewFactoryMethod viewFactory, IInputBlock inputBlock,
            IWorldPermissionsService worldPermissionsService) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.worldPermissionsService = worldPermissionsService;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.PasswordConfirmButton.onClick.AddListener(OnPasswordConfirmClicked);
        }

        protected override void OnBeforeViewShow()
        {
            closeTaskCompletionSource = new UniTaskCompletionSource();
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
            validateCts?.SafeCancelAndDispose();
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
            viewInstance?.ResetState();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (inputData.Mode != PrivateWorldPopupMode.PasswordRequired)
            {
                await UniTask.WhenAny(
                    viewInstance!.InvitationConfirmButton.OnClickAsync(ct),
                    viewInstance.BackgroundCloseButton.OnClickAsync(ct));
                inputData.Result = PrivateWorldPopupResult.Cancelled;
                inputData.EnteredPassword = null;
                return;
            }

            int index = await UniTask.WhenAny(
                viewInstance!.PasswordCancelButton.OnClickAsync(ct),
                viewInstance.BackgroundCloseButton.OnClickAsync(ct),
                closeTaskCompletionSource.Task);

            if (index < 2)
            {
                inputData.Result = PrivateWorldPopupResult.Cancelled;
                inputData.EnteredPassword = null;
            }
        }

        private void OnPasswordConfirmClicked()
        {
            if (inputData.Mode != PrivateWorldPopupMode.PasswordRequired)
                return;
            validateCts = validateCts.SafeRestart();
            ValidatePasswordInternalAsync(validateCts.Token).Forget();
        }

        private async UniTaskVoid ValidatePasswordInternalAsync(CancellationToken ct)
        {
            string password = viewInstance!.EnteredPassword;
            viewInstance.SetValidating(true);

            try
            {
                ValidatePasswordResult result = await worldPermissionsService.ValidatePasswordAsync(
                    inputData.WorldName, password ?? string.Empty, ct);

                viewInstance.SetValidating(false);

                if (result.Success)
                {
                    inputData.Result = PrivateWorldPopupResult.PasswordSubmitted;
                    inputData.EnteredPassword = password;
                    closeTaskCompletionSource.TrySetResult();
                    return;
                }

                string errorMsg = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ErrorMessage
                    : "Incorrect password. Please try again.";
                viewInstance.ShowPasswordError(errorMsg);
            }
            catch (OperationCanceledException)
            {
                viewInstance.SetValidating(false);
            }
            catch (Exception e)
            {
                viewInstance.SetValidating(false);
                viewInstance.ShowPasswordError("An error occurred. Please try again.");
                ReportHub.LogException(e, ReportCategory.REALM);
            }
        }

        public override void Dispose()
        {
            validateCts?.SafeCancelAndDispose();
            if (viewInstance != null)
                viewInstance.PasswordConfirmButton.onClick.RemoveListener(OnPasswordConfirmClicked);
        }
    }
}
