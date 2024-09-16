using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

#if !UNITY_EDITOR
using UnityEngine;
#endif

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
            viewInstance.CloseButton.onClick.AddListener(Quit);
            viewInstance.CloseWithLauncherButton.onClick.AddListener(HandleVersionUpdate);
        }

        public override void Dispose()
        {
            viewInstance.CloseButton.onClick.RemoveListener(Quit);
            viewInstance.CloseWithLauncherButton.onClick.RemoveListener(HandleVersionUpdate);
        }

        private void HandleVersionUpdate()
        {
            versionGuard.LaunchOrDownloadLauncherAsync().Forget();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
