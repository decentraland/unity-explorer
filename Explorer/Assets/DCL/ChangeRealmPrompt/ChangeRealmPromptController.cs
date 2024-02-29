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
        private Action<ChangeRealmPromptResultType> resultCallback;

        public ChangeRealmPromptController(ViewFactoryMethod viewFactory, ICursor cursor) : base(viewFactory)
        {
            this.cursor = cursor;
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
            RequestChangeRealm(inputData.Realm, result =>
            {
                if (result != ChangeRealmPromptResultType.Approved)
                    return;

                inputData.ChangeRealmCallback?.Invoke();
            });
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.ContinueButton.OnClickAsync(ct));

        private void RequestChangeRealm(string realm, Action<ChangeRealmPromptResultType> result)
        {
            resultCallback = result;
            viewInstance.RealmText.text = realm;
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Approved);
    }
}
