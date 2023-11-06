using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.ExplorePanel
{
    public class PersistentExplorePanelOpenerController : ControllerBase<PersistentExploreOpenerView, EmptyParameter>
    {
        private readonly IMVCManager mvcManager;

        public PersistentExplorePanelOpenerController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) => UniTask.CompletedTask;

        protected override void OnViewShow()
        {
            viewInstance.OpenExploreButton.onClick.RemoveAllListeners();
            viewInstance.OpenExploreButton.onClick.AddListener(() =>
                mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(null))).Forget());
        }

    }
}
