using Cysharp.Threading.Tasks;
using DCL.Input;
using MVC;
using System;
using System.Threading;

namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController : ControllerBase<ChangeRealmPromptView, ChangeRealmPromptController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;
        private readonly Action<string> changeRealmCallback;
        private Action<ChangeRealmPromptResultType> resultCallback;

        public ChangeRealmPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor,
            Action<string> changeRealmCallback) : base(viewFactory)
        {
            this.cursor = cursor;
            this.changeRealmCallback = changeRealmCallback;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.CloseButton.onClick.AddListener(Dismiss);
            viewInstance.CancelButton.onClick.AddListener(Dismiss);
            viewInstance.ContinueButton.onClick.AddListener(Approve);
        }

        protected override void OnViewShow()
        {
            cursor.Unlock();
            RequestChangeRealm(inputData.Message, inputData.Realm, result =>
            {
                if (result != ChangeRealmPromptResultType.Approved)
                    return;

                changeRealmCallback?.Invoke(inputData.Realm);
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(inputData.Message))
                return UniTask.CompletedTask;

            return UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));
        }

        private void RequestChangeRealm(string message, string realm, Action<ChangeRealmPromptResultType> result)
        {
            resultCallback = result;

            if (string.IsNullOrEmpty(message))
                resultCallback?.Invoke(ChangeRealmPromptResultType.Approved);
            else
            {
                viewInstance.MessageText.text = message;
                viewInstance.RealmText.text = realm;
            }
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Approved);
    }
}
