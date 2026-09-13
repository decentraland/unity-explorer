using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.SkyBox;
using ECS.SceneLifeCycle;
using Global.Versioning;
using System.Threading;
using Utility;

namespace DCL.PerformanceBenchmark
{
    /// <summary>
    ///     Registered only when the application starts with <c>--plaza-bench &lt;outputDir&gt;</c>.
    ///     Kicks off the plaza-recording benchmark flow (see <see cref="PlazaBenchRunner" />), which
    ///     writes its outputs into the given directory and quits the application when done.
    /// </summary>
    public class PlazaBenchPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly string outputDirectory;
        private readonly World world;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IScenesCache scenesCache;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly IProfiler profiler;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly DCLVersion dclVersion;
        private readonly bool lockstepTime;
        private readonly string? anchorArg;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public PlazaBenchPlugin(string outputDirectory,
            World world,
            IReadOnlyLoadingStatus loadingStatus,
            IScenesCache scenesCache,
            SkyboxSettingsAsset skyboxSettings,
            IProfiler profiler,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            bool lockstepTime,
            string? anchorArg)
        {
            this.outputDirectory = outputDirectory;
            this.world = world;
            this.loadingStatus = loadingStatus;
            this.scenesCache = scenesCache;
            this.skyboxSettings = skyboxSettings;
            this.profiler = profiler;
            this.coroutineRunner = coroutineRunner;
            this.dclVersion = dclVersion;
            this.lockstepTime = lockstepTime;
            this.anchorArg = anchorArg;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            var runner = new PlazaBenchRunner(outputDirectory, world, arguments.PlayerEntity, arguments.SkyboxEntity,
                loadingStatus, scenesCache, skyboxSettings, profiler, coroutineRunner, dclVersion, lockstepTime, anchorArg);

            runner.RunAsync(cancellationTokenSource.Token).Forget();
        }
    }
}
