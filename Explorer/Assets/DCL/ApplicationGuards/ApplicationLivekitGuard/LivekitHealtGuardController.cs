using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using JetBrains.Annotations;
using MVC;
using System.Threading;

public class LivekitHealtGuardController : ControllerBase<LivekitHealthGuardView>
{
    public override CanvasOrdering.SortingLayer Layer { get; }

    public LivekitHealtGuardController([NotNull] ViewFactoryMethod viewFactory) : base(viewFactory) { }


    protected override void OnViewInstantiated()
    {
        viewInstance.ExitButton.onClick.AddListener(GuardUtils.Exit);
    }

    protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
        UniTask.Never(ct);

}
