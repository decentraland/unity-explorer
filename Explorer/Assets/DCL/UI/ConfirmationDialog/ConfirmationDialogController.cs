using Cysharp.Threading.Tasks;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;

namespace DCL.UI.ConfirmationDialog
{
    public class ConfirmationDialogController : ControllerBase<ConfirmationDialogView, ConfirmationDialogParameter>
    {
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public ConfirmationDialogController(ViewFactoryMethod viewFactory,
            ProfileRepositoryWrapper profileRepositoryWrapper)
            : base(viewFactory)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.Configure(inputData, profileRepositoryWrapper);
        }

        protected override void OnViewClose()
        {
            viewInstance!.Reset();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            int index = await UniTask.WhenAny(viewInstance!.GetCloseTasks(ct));

            inputData.ResultCallback?.Invoke(index > 1 ? ConfirmationResult.CONFIRM : ConfirmationResult.CANCEL);
        }
    }
}
