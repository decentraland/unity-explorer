using Cysharp.Threading.Tasks;
using MVC;
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
            viewInstance!.BlockButton.onClick.RemoveAllListeners();
            viewInstance!.UnblockButton.onClick.RemoveAllListeners();
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
                await ManageFriendshipStatusBeforeBlockingAsync(ct);
                //TODO: await user block request

                ClosePopup();
            }
        }

        private async UniTask ManageFriendshipStatusBeforeBlockingAsync(CancellationToken ct)
        {
            FriendshipStatus friendshipStatus = await friendsService.GetFriendshipStatusAsync(inputData.TargetUserId, ct);

            switch (friendshipStatus)
            {
                case FriendshipStatus.FRIEND:
                    await friendsService.DeleteFriendshipAsync(inputData.TargetUserId, ct);
                    break;
                case FriendshipStatus.REQUEST_SENT:
                    await friendsService.CancelFriendshipAsync(inputData.TargetUserId, ct);
                    break;
                case FriendshipStatus.REQUEST_RECEIVED:
                    await friendsService.RejectFriendshipAsync(inputData.TargetUserId, ct);
                    break;
            }
        }

        private void UnblockClicked()
        {
            blockOperationsCts = blockOperationsCts.SafeRestart();
            UnblockUserAsync(blockOperationsCts.Token).Forget();

            async UniTaskVoid UnblockUserAsync(CancellationToken ct)
            {
                //TODO: await user unblock request

                ClosePopup();
            }
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.CancelButton.OnClickAsync(ct), viewInstance!.BackgroundCloseButton.OnClickAsync(ct), closePopupTask.Task);
    }
}
