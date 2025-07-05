using Cysharp.Threading.Tasks;
using DCL.ApplicationGuards;
using MVC;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    public class LauncherRedirectionScreenController : ControllerBase<LauncherRedirectionScreenView>
    {
        private readonly ApplicationVersionGuard.ApplicationVersionGuard versionGuard;
        private readonly string current;
        private readonly string latest;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public LauncherRedirectionScreenController(ApplicationVersionGuard.ApplicationVersionGuard versionGuard, ViewFactoryMethod viewFactory, string current, string latest) : base(viewFactory)
        {
            this.versionGuard = versionGuard;
            this.current = current;
            this.latest = latest;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.SetVersions(current, latest);
            viewInstance.CloseButton.onClick.AddListener(GuardUtils.Exit);
            viewInstance.CloseWithLauncherButton.onClick.AddListener(HandleVersionUpdate);
        }

        public override void Dispose()
        {
            viewInstance.CloseButton.onClick.RemoveListener(GuardUtils.Exit);
            viewInstance.CloseWithLauncherButton.onClick.RemoveListener(HandleVersionUpdate);
        }

        private void HandleVersionUpdate()
        {
            versionGuard.LaunchOrDownloadLauncherAsync().Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
