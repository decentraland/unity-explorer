using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.ApplicationGuards
{
    public class LivekitHealthGuardController : ControllerBase<LivekitHealthGuardView>
    {
        // Overlay draws above the splash screen, which is still up while the guard blocks the bootstrap
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public LivekitHealthGuardController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewInstantiated()
        {
            viewInstance.ExitButton.onClick.AddListener(ExitUtils.Exit);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

    }
}
