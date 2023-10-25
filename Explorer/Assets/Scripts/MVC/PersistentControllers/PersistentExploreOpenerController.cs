using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

public class PersistentExploreOpenerController : ControllerBase<PersistentExploreOpenerView, MVCCheetSheet.ExampleParam>
{
    private readonly IMVCManager mvcManager;

    public PersistentExploreOpenerController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
    {
        this.mvcManager = mvcManager;
    }

    public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Persistent;

    protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
        viewInstance.CloseButton.OnClickAsync(ct);

    protected override void OnViewShow()
    {
        viewInstance.OpenExploreButton.onClick.RemoveAllListeners();
        viewInstance.OpenExploreButton.onClick.AddListener(() =>
            mvcManager.Show(ExplorePanelController.IssueCommand(new MVCCheetSheet.ExampleParam("TEST"))).Forget());
    }

}
