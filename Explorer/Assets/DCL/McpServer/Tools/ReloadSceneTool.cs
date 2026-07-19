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

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Reloads the scene at the player's parcel, freezing character motion and the skybox during the reload
    ///     exactly like the local-scene-development hot reload does.
    /// </summary>
    public class ReloadSceneTool : IMcpTool
    {
        private const float MIN_TIMEOUT_SEC = 5f;
        private const float MAX_TIMEOUT_SEC = 120f;
        private const float DEFAULT_TIMEOUT_SEC = 15f;

        private readonly ECSReloadScene reloadScene;
        private readonly IScenesCache scenesCache;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly Entity skyboxEntity;

        public string Name => "reload_scene";

        public string Description =>
            "Reload the scene at the player's current parcel and wait for it to restart. Use after editing scene code "
            + "when hot reload didn't trigger, or to reset scene state before a test run.";

        public JObject InputSchema =>
            McpJsonSchema.Object()
                          .Number("timeoutSec", "Maximum seconds to wait for the reload. Default 15.")
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: true, idempotent: false);

        public ReloadSceneTool(ECSReloadScene reloadScene, IScenesCache scenesCache, World world, Entity playerEntity, Entity skyboxEntity)
        {
            this.reloadScene = reloadScene;
            this.scenesCache = scenesCache;
            this.world = world;
            this.playerEntity = playerEntity;
            this.skyboxEntity = skyboxEntity;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            if (scenesCache.CurrentScene.Value == null)
                return McpToolResult.Error("There is no scene at the current parcel to reload.");

            try
            {
                world.AddOrGet(playerEntity, new StopCharacterMotion());
                world.AddOrGet(skyboxEntity, new PauseSkyboxTimeUpdate());

                ISceneFacade? reloadedScene = await reloadScene.TryReloadSceneAsync(ct)
                                                               .Timeout(TimeSpan.FromSeconds(timeoutSec));

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

            return McpToolResult.Text("Scene reloaded. Call get_scene_state to confirm readiness and get_scene_logs for startup output.");
        }
    }
}
