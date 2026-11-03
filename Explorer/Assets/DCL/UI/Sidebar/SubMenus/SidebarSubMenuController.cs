using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.UI.Sidebar.SubMenus
{
    /// <summary>
    ///     Hosts a sidebar submenu as an MVC popup so the shared popup closer dismisses it on an outside click.
    ///     The submenu contents stay wired on <see cref="SidebarView" />; this only owns the show/hide lifecycle.
    /// </summary>
    public class SidebarSubMenuController<TView> : ControllerBase<TView> where TView: ViewBase, IView
    {
        public SidebarSubMenuController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
