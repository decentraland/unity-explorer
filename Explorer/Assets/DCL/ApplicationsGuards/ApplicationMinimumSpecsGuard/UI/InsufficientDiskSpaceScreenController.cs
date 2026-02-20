using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.ApplicationMinimumSpecsGuard
{
    public class InsufficientDiskSpaceScreenController : ControllerBase<InsufficientDiskSpaceScreenView>
    {

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public InsufficientDiskSpaceScreenController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewInstantiated()
        {
            if (viewInstance != null)
                viewInstance.QuitButton.onClick.AddListener(ExitUtils.Exit);
        }

        public override void Dispose()
        {
            if (viewInstance == null)
                return;

            viewInstance.QuitButton.onClick.RemoveListener(ExitUtils.Exit);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
