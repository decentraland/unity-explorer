using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI.ErrorPopup
{
    public partial class ErrorPopupController : ControllerBase<ErrorPopupView, ErrorPopupData>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public static ViewFactoryMethod CreateLazily(ErrorPopupView prefab, Transform? root = null) =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, root!)!;

        public ErrorPopupController(ErrorPopupView prefab) : this(CreateLazily(prefab)) { }

        public ErrorPopupController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewShow()
        {
            viewInstance.Apply(inputData);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance!.OkButton.OnClickAsync(ct);
    }
}
