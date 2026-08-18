using DCL.BugReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.SceneLifeCycle;
using Global.AppArgs;

namespace DCL.PluginSystem.Global
{
    public class BugReportSessionContext : IBugReportSessionContext
    {
        private readonly IScenesCache scenesCache;

        public BugReportSessionContext(IScenesCache scenesCache, IAppArgs appArgs)
        {
            this.scenesCache = scenesCache;
            LauncherVersion = appArgs.TryGetValue(AppArgsFlags.Launcher.VERSION, out string? version) && !string.IsNullOrWhiteSpace(version) ? version : null;
        }

        public bool? MeetsMinimumSpecs => UnityDiagnosticsCenter.Instance.MeetsMinimumRequirements;

        public string? SceneSdkVersion
        {
            get
            {
                string? version = scenesCache.CurrentScene.Value?.SceneData.GetSDKVersion();
                return string.IsNullOrEmpty(version) ? null : version;
            }
        }

        public string? LauncherVersion { get; }
    }
}
