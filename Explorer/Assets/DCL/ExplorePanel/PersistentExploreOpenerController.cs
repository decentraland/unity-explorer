using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.ExplorePanel
{
    public class PersistentExploreOpenerController : ControllerBase<PersistentExploreOpenerView, EmptyParameter>
    {
        private readonly IMVCManager mvcManager;

        public PersistentExploreOpenerController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);

        protected override void OnViewShow()
        {
            viewInstance.OpenExploreButton.onClick.RemoveAllListeners();
            viewInstance.OpenExploreButton.onClick.AddListener(() =>
                mvcManager.Show(ExplorePanelController.IssueCommand(new EmptyParameter())).Forget());
        }

    }
}
