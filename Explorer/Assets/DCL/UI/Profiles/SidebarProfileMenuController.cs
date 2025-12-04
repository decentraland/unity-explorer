using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.Profiles
{
    public class SidebarProfileMenuController : ControllerBase<ProfileMenuView>
    {
        public SidebarProfileMenuController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer { get; } = CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
