using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.Infrastructure.Global
{
    public class UntrustedRealmConfirmationController : ControllerBase<UntrustedRealmConfirmationView, UntrustedRealmConfirmationController.Args>
    {
        private UniTaskCompletionSource? lifeCycleTask;
        private string expectedCatalystName = string.Empty;

        public bool SelectedOption { get; private set; }

        public UntrustedRealmConfirmationController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override void OnViewInstantiated()
        {
            viewInstance!.ContinueButton.onClick.AddListener(Continue);
            viewInstance!.QuitButton.onClick.AddListener(Cancel);
            viewInstance!.CatalystNameInput.onValueChanged.AddListener(OnCatalystNameChanged);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            expectedCatalystName = CatalystNameFrom(inputData.realm);

            viewInstance!.RealmLabel.text = $"In order to continue, please type <b>'{expectedCatalystName}'</b> below:";
            viewInstance!.CatalystNameInput.SetTextWithoutNotify(string.Empty);
            viewInstance!.ContinueButton.interactable = false;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask ??= new UniTaskCompletionSource();
            return lifeCycleTask.Task;
        }

        private void OnCatalystNameChanged(string typedName)
        {
            viewInstance!.ContinueButton.interactable = MatchesExpectedCatalystName(typedName);
        }

        private void Continue()
        {
            if (!MatchesExpectedCatalystName(viewInstance!.CatalystNameInput.text))
                return;

            SelectedOption = true;
            lifeCycleTask!.TrySetResult();
        }

        private void Cancel()
        {
            SelectedOption = false;
            lifeCycleTask!.TrySetResult();
        }

        private bool MatchesExpectedCatalystName(string typedName)
        {
            typedName = typedName.Trim();

            return string.Equals(typedName, expectedCatalystName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(typedName, inputData.realm.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string CatalystNameFrom(string realm) =>
            Uri.TryCreate(realm, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.Host
                : realm;

        public struct Args
        {
            public string realm;
        }
    }
}
