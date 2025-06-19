using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using MVC;
using System;
using System.Threading;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Friends.UI.BlockUserPrompt
{
    public class BlockUserPromptController : ControllerBase<BlockUserPromptView, BlockUserPromptParams>
    {
        private readonly IFriendsService friendsService;
        private readonly DCLInput dclInput;

        private UniTaskCompletionSource closePopupTask = new ();
        private CancellationTokenSource blockOperationsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public BlockUserPromptController(ViewFactoryMethod viewFactory,
            IFriendsService friendsService)
            : base(viewFactory)
        {
            this.friendsService = friendsService;
            dclInput = DCLInput.Instance;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance?.BlockButton.onClick.RemoveAllListeners();
            viewInstance?.UnblockButton.onClick.RemoveAllListeners();
            dclInput.UI.Close.performed -= ClosePopup;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.BlockButton.onClick.AddListener(BlockClicked);
            viewInstance!.UnblockButton.onClick.AddListener(UnblockClicked);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            closePopupTask = new UniTaskCompletionSource();

            viewInstance!.ConfigureButtons(inputData.Action);
            viewInstance.SetTitle(inputData.Action, inputData.TargetUserName);

            dclInput.UI.Close.performed += ClosePopup;
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            dclInput.UI.Close.performed -= ClosePopup;
        }

        private void ClosePopup(InputAction.CallbackContext obj) =>
            ClosePopup();

        private void ClosePopup() =>
            closePopupTask.TrySetResult();

        private void BlockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            BlockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid BlockUserAsync(CancellationToken ct)
            {
                await friendsService.BlockUserAsync(inputData.TargetUserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
                ClosePopup();
            }
        }

        private void UnblockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            UnblockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid UnblockUserAsync(CancellationToken ct)
            {
                await friendsService.UnblockUserAsync(inputData.TargetUserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);
                ClosePopup();
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.CancelButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closePopupTask.Task);
    }
}
