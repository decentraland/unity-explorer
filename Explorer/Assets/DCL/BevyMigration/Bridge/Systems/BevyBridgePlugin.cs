using Arch.Core;
using Arch.SystemGroups;
using DCL.BevyMigration.Composite;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.PluginSystem.Global;
using Global.AppArgs;
using UnityEngine;

namespace DCL.BevyMigration.Bridge.Systems
{
    /// <summary>
    ///     The one hook the composition root has into the migration (BevyMigration/docs/README.md § 4): reads the
    ///     `world` app argument, owns the registry and the native bridge for the global world's lifetime, exposes
    ///     the runtime flag flip and the step 2 test cube through the debug panel, and injects the frame system.
    /// </summary>
    public class BevyBridgePlugin : IDCLGlobalPluginWithoutSettings
    {
        private const string TEST_CUBE_NAME = "[BevyMigration] Test Cube";
        private const float TEST_CUBE_DISTANCE = 4f;

        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);

        private readonly IDebugContainerBuilder debugBuilder;
        private readonly RendererRegistry registry = new ();
        private readonly ResourceUploader uploader = new ();
        private readonly SnapshotWriter writer;
        private readonly SceneContentScanner scanner;
        private readonly AvatarContentScanner avatars;
        private readonly ElementBinding<string> statusBinding = new (string.Empty);
        private readonly DebugWidgetVisibilityBinding visibility = new (true);
        private readonly WorldRenderer initialRenderer;
        private readonly bool spawnTestCubeAtStart;

        private GameObject? testCube;

        public BevyBridgePlugin(IAppArgs appArgs, IDebugContainerBuilder debugBuilder)
        {
            this.debugBuilder = debugBuilder;
            writer = new SnapshotWriter(registry);
            scanner = new SceneContentScanner(registry, uploader);
            avatars = new AvatarContentScanner(registry, uploader);

            appArgs.TryGetValue(AppArgsFlags.WORLD, out string? world);
            WorldRendererFlag.TryParse(world, out initialRenderer);
            spawnTestCubeAtStart = appArgs.HasFlag(AppArgsFlags.BEVY_TEST_CUBE);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            BevyBridgeRuntime.Registry = registry;

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.BEVY_MIGRATION)
                       ?.SetVisibilityBinding(visibility)
                        .AddToggleField("World renderer: Bevy", evt => SetWorldRenderer(evt.newValue ? WorldRenderer.Bevy : WorldRenderer.Unity), initialRenderer == WorldRenderer.Bevy)
                        .AddToggleField("Flip composite vertically", evt => BevyComposite.FlipVertically = evt.newValue, BevyComposite.FlipVertically)
                        .AddSingleButton("Spawn test cube", SpawnTestCube)
                        .AddSingleButton("Remove test cube", RemoveTestCube)
                        .AddCustomMarker("Bridge:", statusBinding);

            BevyBridgeSystem.InjectToWorld(ref builder, registry, writer, scanner, avatars, statusBinding, visibility);

            if (spawnTestCubeAtStart)
                SpawnTestCube();

            SetWorldRenderer(initialRenderer);
        }

        public void Dispose()
        {
            RemoveTestCube();
            BevyBridge.Shutdown();
            BevyBridgeRuntime.Registry = null;
            writer.Dispose();
            registry.Dispose();
        }

        private void SetWorldRenderer(WorldRenderer renderer)
        {
            if (renderer == WorldRenderer.Bevy && !BevyBridge.IsInitialized && !BevyBridge.Initialize(Screen.width, Screen.height))
            {
                ReportHub.LogWarning(REPORT_DATA, "world=bevy requested but the bridge could not start; staying on Unity");
                WorldRendererFlag.Set(WorldRenderer.Unity);
                return;
            }

            WorldRendererFlag.Set(renderer);
        }

        /// <summary>
        ///     Gate G-A1's cube: URP Lit, in front of the camera, registered with the bridge.
        /// </summary>
        private void SpawnTestCube()
        {
            if (testCube != null) return;

            Camera? camera = Camera.main;
            testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = TEST_CUBE_NAME;
            testCube.transform.position = camera != null ? camera.transform.position + (camera.transform.forward * TEST_CUBE_DISTANCE) : new Vector3(0f, 1f, TEST_CUBE_DISTANCE);
            UnityEngine.Object.Destroy(testCube.GetComponent<Collider>());
            testCube.AddComponent<BevyRenderedObject>();

            if (WorldRendererFlag.Current == WorldRenderer.Bevy)
                registry.SetUnityRenderingEnabled(false);
        }

        private void RemoveTestCube()
        {
            if (testCube == null) return;

            UnityEngine.Object.Destroy(testCube);
            testCube = null;
        }
    }
}
