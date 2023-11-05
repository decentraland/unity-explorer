using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;

namespace DCL.ExplorePanel
{
    public class MinimapController : ControllerBase<MinimapView, EmptyParameter>
    {
        private readonly IMVCManager mvcManager;

        public MinimapController(ViewFactoryMethod viewFactory, IMVCManager mvcManager) : base(viewFactory)
        {
            this.mvcManager = mvcManager;
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntent(CancellationToken ct) => UniTask.CompletedTask;

        protected override void OnViewShow()
        {
            viewInstance.OpenExploreMapButton.onClick.RemoveAllListeners();
            viewInstance.OpenExploreMapButton.onClick.AddListener(() =>
                mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Navmap))).Forget());
        }

    }
}
