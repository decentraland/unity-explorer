using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    public class LauncherRedirectionScreenController: ControllerBase<LauncherRedirectionScreenView>
    {
        public LauncherRedirectionScreenController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer { get; }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
