using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;
#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace DCL.ApplicationGuards
{
    public class LauncherRedirectionScreenController : ControllerBase<LauncherRedirectionScreenView>
    {
        private readonly ApplicationVersionGuard versionGuard;
        private readonly string current;
        private readonly string latest;

        private UniTaskCompletionSource closeIntent = null!;

        public bool UpdateSkipped { get; private set; }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public LauncherRedirectionScreenController(ApplicationVersionGuard versionGuard, ViewFactoryMethod viewFactory, string current, string latest) : base(viewFactory)
        {
            this.versionGuard = versionGuard;
            this.current = current;
            this.latest = latest;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.SetVersions(current, latest);
            viewInstance.CloseButton.onClick.AddListener(ExitUtils.Exit);
            viewInstance.CloseWithLauncherButton.onClick.AddListener(HandleVersionUpdate);

            if (viewInstance.ContinueAnywayButton != null)
                viewInstance.ContinueAnywayButton.onClick.AddListener(HandleContinueAnyway);
        }

        protected override void OnViewShow()
        {
            closeIntent = new UniTaskCompletionSource();
        }

        public override void Dispose()
        {
            viewInstance.CloseButton.onClick.RemoveListener(ExitUtils.Exit);
            viewInstance.CloseWithLauncherButton.onClick.RemoveListener(HandleVersionUpdate);

            if (viewInstance.ContinueAnywayButton != null)
                viewInstance.ContinueAnywayButton.onClick.RemoveListener(HandleContinueAnyway);
        }

        private void HandleVersionUpdate()
        {
            versionGuard.LaunchOrDownloadLauncherAsync().Forget();
        }

        private void HandleContinueAnyway()
        {
            UpdateSkipped = true;
            closeIntent.TrySetResult();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            closeIntent.Task.AttachExternalCancellation(ct);
    }
}
