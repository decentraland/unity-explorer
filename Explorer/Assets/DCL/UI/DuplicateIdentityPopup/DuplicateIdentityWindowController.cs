using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowController : ControllerBase<DuplicateIdentityWindowView>
    {
        public DuplicateIdentityWindowController(
            ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnBeforeViewShow()
        {
            viewInstance!.ExitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnExitButtonClicked()
        {
            ExitUtils.Exit();
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public static ViewFactoryMethod CreateLazily(DuplicateIdentityWindowView prefab) =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) => UniTask.Never(ct);
    }
}


