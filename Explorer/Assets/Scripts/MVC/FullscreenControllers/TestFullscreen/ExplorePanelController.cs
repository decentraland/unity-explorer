using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

public class ExplorePanelController : ControllerBase<ExplorePanelView, MVCCheetSheet.ExampleParam>
{
    public ExplorePanelController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Fullscreen;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

}
