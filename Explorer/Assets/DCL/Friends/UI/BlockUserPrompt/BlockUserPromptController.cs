using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities.Extensions;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Friends.UI.BlockUserPrompt
{
    public class BlockUserPromptController : ControllerBase<BlockUserPromptView, BlockUserPromptParams>
    {
        private const string SUCCESS_BLOCK_NOTIFICATION_TEXT = "User <b>{0}</b> has been successfully blocked.";
        private const string ERROR_BLOCK_NOTIFICATION_TEXT = "Something went wrong while blocking user <b>{0}</b>";
        private const string SUCCESS_UNBLOCK_NOTIFICATION_TEXT = "User <b>{0}</b> has been successfully unblocked.";
        private const string ERROR_UNBLOCK_NOTIFICATION_TEXT = "Something went wrong while unblocking user <b>{0}</b>";

        private readonly IFriendsService friendsService;

        private UniTaskCompletionSource closePopupTask = new ();
        private CancellationTokenSource blockOperationsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

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
                var result = await friendsService.BlockUserAsync(inputData.TargetUserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                NotificationsBusController.Instance.AddNotification(
                    result.Success
                        ? new DefaultSuccessNotification(string.Format(SUCCESS_BLOCK_NOTIFICATION_TEXT, inputData.TargetUserName))
                        : new ServerErrorNotification(string.Format(ERROR_BLOCK_NOTIFICATION_TEXT, inputData.TargetUserName)));

                ClosePopup();
            }
        }

        private void UnblockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            UnblockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid UnblockUserAsync(CancellationToken ct)
            {
                var result = await friendsService.UnblockUserAsync(inputData.TargetUserId, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

                NotificationsBusController.Instance.AddNotification(
                    result.Success
                        ? new DefaultSuccessNotification(string.Format(SUCCESS_UNBLOCK_NOTIFICATION_TEXT, inputData.TargetUserName))
                        : new ServerErrorNotification(string.Format(ERROR_UNBLOCK_NOTIFICATION_TEXT, inputData.TargetUserName)));

                ClosePopup();
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.CancelButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closePopupTask.Task);
    }
}
