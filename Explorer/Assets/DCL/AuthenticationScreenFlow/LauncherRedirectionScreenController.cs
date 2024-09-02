using Cysharp.Threading.Tasks;
using Global.Dynamic;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow
{
    public class LauncherRedirectionScreenController: ControllerBase<LauncherRedirectionScreenView>
    {
        private readonly string current;
        private readonly string latest;

        public LauncherRedirectionScreenController(ViewFactoryMethod viewFactory, string current, string latest) : base(viewFactory)
        {
            this.current = current;
            this.latest = latest;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance.SetVersions(current, latest);
            viewInstance.CloseButton.onClick.AddListener(Application.Quit);
            viewInstance.CloseWithLauncherButton.onClick.AddListener(ApplicationVersionGuard.LaunchExternalAppAndQuit);
        }

        public override void Dispose()
        {
            base.Dispose();
            viewInstance.CloseButton.onClick.RemoveListener(Application.Quit);
            viewInstance.CloseWithLauncherButton.onClick.RemoveListener(ApplicationVersionGuard.LaunchExternalAppAndQuit);
        }

        // protected override void OnViewShow()
        // {
        //     viewInstance.SetVersions();
        //     base.OnViewShow();
        // }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
