using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.BlockUserPrompt
{
    public class BlockUserPromptController : ControllerBase<BlockUserPromptView, BlockUserPromptParams>
    {
        private readonly IFriendsService friendsService;

        private UniTaskCompletionSource closePopupTask = new ();
        private CancellationTokenSource blockOperationsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public BlockUserPromptController(ViewFactoryMethod viewFactory,
            IFriendsService friendsService)
            : base(viewFactory)
        {
            this.friendsService = friendsService;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewInstance?.BlockButton.onClick.RemoveAllListeners();
            viewInstance?.UnblockButton.onClick.RemoveAllListeners();
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
        }

        private void ClosePopup() =>
            closePopupTask.TrySetResult();

        private void BlockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            BlockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid BlockUserAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.BlockUserAsync(inputData.TargetUserId, ct);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
                finally
                {
                    ClosePopup();
                }
            }
        }

        private void UnblockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            UnblockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid UnblockUserAsync(CancellationToken ct)
            {
                try
                {
                    await friendsService.UnblockUserAsync(inputData.TargetUserId, ct);
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                }
                finally
                {
                    ClosePopup();
                }
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.CancelButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closePopupTask.Task);
    }
}
