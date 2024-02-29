using CommunicationData.URLHelpers;
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
        //private readonly IRealmController realmController;
        private Action<ChangeRealmPromptResultType> resultCallback;

        public ChangeRealmPromptController(
            ViewFactoryMethod viewFactory,
            ICursor cursor/*,
            IRealmController realmController*/) : base(viewFactory)
        {
            this.cursor = cursor;
            //this.realmController = realmController;
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

                ChangeRealmAsync(inputData.Realm, CancellationToken.None).Forget();
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

        private async UniTask ChangeRealmAsync(string realmUrl, CancellationToken ct)
        {
            //return await realmController.SetRealmAsync(URLDomain.FromString(realmUrl), ct);
        }

        private void Dismiss() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Canceled);

        private void Approve() =>
            resultCallback?.Invoke(ChangeRealmPromptResultType.Approved);
    }
}
