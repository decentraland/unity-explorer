using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.Infrastructure.Global
{
    public class UntrustedRealmConfirmationController : ControllerBase<UntrustedRealmConfirmationView, UntrustedRealmConfirmationController.Args>
    {
        private UniTaskCompletionSource? lifeCycleTask;

        public bool SelectedOption { get; private set; }

        public UntrustedRealmConfirmationController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override void OnViewInstantiated()
        {
            viewInstance!.ContinueButton.onClick.AddListener(Continue);
            viewInstance!.CloseButton.onClick.AddListener(Continue);
            viewInstance!.QuitButton.onClick.AddListener(Cancel);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            viewInstance!.RealmLabel.text = $"Are you sure you trust <b>'{inputData.realm}'</b>?";
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask ??= new UniTaskCompletionSource();
            return lifeCycleTask.Task;
        }

        private void Continue()
        {
            SelectedOption = true;
            lifeCycleTask!.TrySetResult();
        }

        private void Cancel()
        {
            SelectedOption = false;
            lifeCycleTask!.TrySetResult();
        }

        public struct Args
        {
            public string realm;
        }
    }
}
