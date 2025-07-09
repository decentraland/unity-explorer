using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using MVC;
using System.Threading;

public class LivekitHealtGuardController : ControllerBase<LivekitHealthGuardView>
{
    public override CanvasOrdering.SortingLayer Layer { get; }

    public LivekitHealtGuardController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    protected override void OnViewInstantiated()
    {
        viewInstance.ExitButton.onClick.AddListener(ExitUtils.Exit);
    }

    protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
        UniTask.Never(ct);

}
