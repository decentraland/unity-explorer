using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.McpServer.Core;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Teleports through the same /goto command pipeline a user teleport takes (loading screen included),
    ///     then polls until the destination scene is ready or the timeout elapses.
    /// </summary>
    public class TeleportTool : IMcpTool
    {
        private const int POLL_INTERVAL_MS = 500;
        private const float MIN_TIMEOUT_SEC = 5f;
        private const float MAX_TIMEOUT_SEC = 300f;
        private const float DEFAULT_TIMEOUT_SEC = 60f;

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IScenesCache scenesCache;
        private readonly ILoadingStatus loadingStatus;

        public string Name => "teleport";

        public string Description =>
            "Teleport the player to a parcel (x,y) through the regular /goto flow and wait until the destination scene is ready. "
            + "Reports the final scene state; follow up with get_scene_state for details.";

        public JObject InputSchema =>
            McpInputSchema.Object()
                          .Integer("x", "Target parcel X coordinate.", required: true)
                          .Integer("y", "Target parcel Y coordinate.", required: true)
                          .Boolean("waitForReady", "Wait until the destination scene is ready. Default true.")
                          .Number("timeoutSec", "Maximum seconds to wait for readiness. Default 60.")
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public TeleportTool(IChatMessagesBus chatMessagesBus, IScenesCache scenesCache, ILoadingStatus loadingStatus)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.scenesCache = scenesCache;
            this.loadingStatus = loadingStatus;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetInt("x", out int x) || !arguments.TryGetInt("y", out int y))
                return McpToolResult.Error("Both x and y parcel coordinates are required.");

            bool waitForReady = arguments.GetBool("waitForReady", true);
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            await UniTask.SwitchToMainThread(ct);

            chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, $"/{ChatCommandsUtils.COMMAND_GOTO} {x},{y}", ChatMessageOrigin.RESTRICTED_ACTION_API);

            if (!waitForReady)
                return McpToolResult.Text($"Teleport to ({x},{y}) requested.");

            var targetParcel = new Vector2Int(x, y);
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);

                ISceneFacade? currentScene = scenesCache.CurrentScene.Value;
                bool arrived = scenesCache.CurrentParcel.Value == targetParcel || (currentScene?.Contains(targetParcel) ?? false);

                if (!arrived || loadingStatus.IsLoadingScreenOn())
                    continue;

                if (currentScene == null)
                    return McpToolResult.Text($"Arrived at ({x},{y}); no scene is deployed at this parcel.");

                if (currentScene.SceneStateProvider.IsNotRunningState())
                    return McpToolResult.Error($"Arrived at ({x},{y}) but scene '{currentScene.Info.Name}' is not running: {currentScene.SceneStateProvider.State.Value()}. Check get_scene_logs.");

                if (currentScene.IsSceneReady())
                    return McpToolResult.Text($"Teleported to ({x},{y}). Scene '{currentScene.Info.Name}' is ready.");
            }

            return McpToolResult.Error($"Teleport to ({x},{y}) did not reach a ready scene within {timeoutSec}s. Check get_scene_state.");
        }
    }
}
