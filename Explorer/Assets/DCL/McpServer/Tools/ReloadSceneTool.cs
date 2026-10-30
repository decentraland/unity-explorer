using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SkyBox.Components;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Reloads the scene at the player's parcel, freezing character motion and the skybox during the reload
    ///     exactly like the local-scene-development hot reload does, then waits until the new scene has run and
    ///     rendered, so a screenshot taken right after it shows the reloaded scene rather than the previous frame.
    /// </summary>
    public class ReloadSceneTool : McpTool
    {
        /// <summary>Shape of <see cref="ECSReloadScene.TryReloadSceneAsync(CancellationToken)" />, injectable for tests.</summary>
        internal delegate UniTask<ISceneFacade?> Reload(CancellationToken ct);

        private const float MIN_TIMEOUT_SEC = 5f;
        private const float MAX_TIMEOUT_SEC = 120f;
        private const float DEFAULT_TIMEOUT_SEC = 15f;

        /// <summary>Scene ticks the new scene must run before the reload is reported settled: its first outputs have then been applied.</summary>
        private const int SETTLE_TICKS = 2;

        private readonly Reload reload;
        private readonly IScenesCache scenesCache;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly Entity skyboxEntity;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly SceneSettleWait.WaitForFreshScene waitForFreshScene;

        public override string Name => "reload_scene";

        public override string Description =>
            "Reload the scene at the player's current parcel and wait until the reloaded scene is running, has ticked and has "
            + "rendered a frame, so the next screenshot shows it. Use after editing scene code when hot reload didn't trigger, "
            + "or to reset scene state before a test run. The result reports the new scene's state and how long settling took; "
            + "a warning means the scene did not reach Running within the timeout (check get_scene_state and get_scene_logs).";

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Boolean("reloaded")
                          .String("sceneState", "State of the scene at the parcel once the wait ended, in snake_case (running, java_script_error, ecs_error, engine_error, starting, …).")
                          .Integer("ticksWaited", "Scene ticks the reloaded scene ran before the result was produced.")
                          .Integer("renderedFrames", "Frames rendered after the scene ticked; always 1 once settled.")
                          .Integer("settleMs", "Milliseconds between the reload completing and the scene settling (or the wait giving up).")
                          .Boolean("settled", "True when a different, running scene ticked the required number of times before the timeout.")
                          .String("warning", nullable: true)
                          .String("hint")
                          .Build();

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("timeoutSec", "Maximum seconds to wait for the reload and the reloaded scene to settle. Default 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: true, idempotent: false);

        public ReloadSceneTool(ECSReloadScene reloadScene, IScenesCache scenesCache, World world, Entity playerEntity, Entity skyboxEntity, ICoroutineRunner coroutineRunner)
            : this(reloadScene.TryReloadSceneAsync, scenesCache, world, playerEntity, skyboxEntity, coroutineRunner) { }

        internal ReloadSceneTool(Reload reload, IScenesCache scenesCache, World world, Entity playerEntity, Entity skyboxEntity, ICoroutineRunner coroutineRunner,
            SceneSettleWait.WaitForFreshScene? waitForFreshScene = null)
        {
            this.reload = reload;
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.skyboxEntity = skyboxEntity;
            this.coroutineRunner = coroutineRunner;
            this.waitForFreshScene = waitForFreshScene ?? SceneSettleWait.WaitForFreshSceneAsync;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            ISceneFacade? previous = scenesCache.CurrentScene.Value;

            if (previous == null)
                return McpToolResult.Error("There is no scene at the current parcel to reload.");

            float start = UnityEngine.Time.realtimeSinceStartup;

            try
            {
                world.AddOrGet(playerEntity, new StopCharacterMotion());
                world.AddOrGet(skyboxEntity, new PauseSkyboxTimeUpdate());

                ISceneFacade? reloadedScene = await reload(ct).Timeout(TimeSpan.FromSeconds(timeoutSec));

                if (reloadedScene == null)
                    return McpToolResult.Error("Reload failed: no reloadable scene was found at the current parcel.");
            }
            catch (TimeoutException)
            {
                return McpToolResult.Error($"Scene reload did not complete within {timeoutSec}s. Check get_scene_state and get_scene_logs.");
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                world.Remove<StopCharacterMotion>(playerEntity);
                world.Remove<PauseSkyboxTimeUpdate>(skyboxEntity);
            }

            float remainingSec = Mathf.Max(0f, timeoutSec - (UnityEngine.Time.realtimeSinceStartup - start));
            SceneSettleWait.Result settle = await waitForFreshScene(scenesCache, previous, SETTLE_TICKS, remainingSec, ct);

            // The scene's first ticks are applied to the world during the frame; a frame boundary makes them visible.
            await FrameAwaiter.WaitForEndOfFrameAsync(coroutineRunner, ct);

            var result = new JObject
            {
                ["reloaded"] = true,
                ["sceneState"] = McpWireEnum<SceneState>.ToWire(settle.State),
                ["ticksWaited"] = settle.TicksWaited,
                ["renderedFrames"] = 1,
                ["settleMs"] = settle.WaitedMs,
                ["settled"] = settle.Settled,
                ["warning"] = settle.Settled ? JValue.CreateNull() : DescribeUnsettled(settle),
                ["hint"] = "Call get_scene_logs for startup output.",
            };

            return McpToolResult.JsonWithStructured(result);
        }

        private static string DescribeUnsettled(in SceneSettleWait.Result settle) =>
            settle.State is SceneState.JavaScriptError or SceneState.EcsError or SceneState.EngineError
                ? $"the reloaded scene failed with {McpWireEnum<SceneState>.ToWire(settle.State)}; see get_scene_logs"
                : $"the reloaded scene did not reach Running and tick {SETTLE_TICKS} times within the timeout (state {McpWireEnum<SceneState>.ToWire(settle.State)}, {settle.TicksWaited} ticks); "
                  + "see get_scene_state and get_scene_logs";
    }
}
