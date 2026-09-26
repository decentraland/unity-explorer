using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.ApplicationGuards
{
    public class LivekitHealthGuardController : ControllerBase<LivekitHealthGuardView>
    {
        public override CanvasOrdering.SortingLayer Layer { get; }

        public LivekitHealthGuardController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        protected override void OnViewInstantiated()
        {
            viewInstance.ExitButton.onClick.AddListener(ExitUtils.Exit);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

    }
}
