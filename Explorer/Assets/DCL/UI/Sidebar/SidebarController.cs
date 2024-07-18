using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using MVC;
using System.Threading;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private readonly IMVCManager mvcManager;


        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public SidebarController(ViewFactoryMethod viewFactory)
            : base(viewFactory)
        {
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            return new UniTask();
        }
        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
        }

        protected override void OnViewShow()
        {
        }


        private void OpenBackpack()
        {
            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Emotes)));
        }

    }
}
