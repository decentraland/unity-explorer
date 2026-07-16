using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Mcp.Components;
using DCL.Mcp.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility.Arch;

namespace DCL.Mcp.Tools
{
    /// <summary>
    ///     Presses a pointer button on a scene entity through <see cref="McpPointerClickIntent" /> /
    ///     McpPointerClickSystem, which validates the aim with the same raycast rules as the reticle
    ///     pipeline before filling the entity's pointer-event intent like a real click.
    /// </summary>
    public class ClickEntityTool : IMcpTool
    {
        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;
        private const float COMPLETION_GRACE_SEC = 2f;

        private readonly World world;
        private readonly Entity playerEntity;

        public string Name => "click_entity";

        public string Description =>
            "Press and release a pointer button on a scene entity so its PointerEvents fire exactly like a real click. "
            + "The aim is validated by a physics raycast from the camera: occluders and the entity's maxDistance apply, and a miss "
            + "returns hit:false with the blocking entity. Ids come from list_scene_entities. For entities whose collider "
            + "sits away from their pivot (e.g. GLTF meshes), pass an explicit x/y/z world point to aim at.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""entityId"": { ""type"": ""integer"", ""description"": ""Target entity id in the current scene world (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target."" },
                    ""x"": { ""type"": ""number"", ""description"": ""World-space aim point; overrides the automatic aim at the entity's collider center."" },
                    ""y"": { ""type"": ""number"" },
                    ""z"": { ""type"": ""number"" },
                    ""button"": { ""type"": ""string"", ""enum"": [""pointer"", ""primary"", ""secondary""], ""description"": ""Which input action to press. Default pointer (left click / IA_POINTER)."" },
                    ""eventType"": { ""type"": ""string"", ""enum"": [""click"", ""down"", ""up""], ""description"": ""click = down, then up on the next scene tick. Default click."" },
                    ""timeoutSec"": { ""type"": ""number"", ""description"": ""Seconds to wait for delivery. Default 3, max 15."" }
                }
            }";

        public ClickEntityTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool hasEntityId = arguments.TryGetInt("entityId", out int entityId);

            bool hasAimPoint = arguments.TryGetFloat("x", out float x)
                               & arguments.TryGetFloat("y", out float y)
                               & arguments.TryGetFloat("z", out float z);

            if (!hasEntityId && !hasAimPoint)
                return McpToolResult.Error("Provide entityId, or a full x/y/z world aim point, or both.");

            InputAction button;

            switch (arguments.GetString("button", "pointer"))
            {
                case "pointer": button = InputAction.IaPointer; break;
                case "primary": button = InputAction.IaPrimary; break;
                case "secondary": button = InputAction.IaSecondary; break;
                default: return McpToolResult.Error("button must be one of: pointer, primary, secondary.");
            }

            McpPointerClickIntent.ClickKind kind;

            switch (arguments.GetString("eventType", "click"))
            {
                case "click": kind = McpPointerClickIntent.ClickKind.Click; break;
                case "down": kind = McpPointerClickIntent.ClickKind.Down; break;
                case "up": kind = McpPointerClickIntent.ClickKind.Up; break;
                default: return McpToolResult.Error("eventType must be one of: click, down, up.");
            }

            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            await UniTask.SwitchToMainThread(ct);

            // A newer click preempts a pending one; release its awaiter before replacing the intent.
            if (world.TryGet(playerEntity, out McpPointerClickIntent existingIntent))
                existingIntent.Completion?.TrySetResult(new McpPointerClickResult
                {
                    Hit = false,
                    FailureReason = "preempted by a newer click_entity call",
                });

            var completion = new UniTaskCompletionSource<McpPointerClickResult>();

            world.AddOrSet(playerEntity, new McpPointerClickIntent
            {
                TargetEntityId = hasEntityId ? entityId : -1,
                AimPoint = new Vector3(x, y, z),
                HasExplicitAimPoint = hasAimPoint,
                Button = button,
                Kind = kind,
                Phase = McpPointerClickIntent.ClickPhase.Down,
                Deadline = UnityEngine.Time.time + timeoutSec,
                Completion = completion,
            });

            McpPointerClickResult result;

            try
            {
                result = await completion.Task.AttachExternalCancellation(ct)
                                         .Timeout(TimeSpan.FromSeconds(timeoutSec + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                await UniTask.SwitchToMainThread();

                if (world.Has<McpPointerClickIntent>(playerEntity))
                    world.Remove<McpPointerClickIntent>(playerEntity);

                return McpToolResult.Error($"click_entity did not complete within {timeoutSec + COMPLETION_GRACE_SEC}s (is the simulation paused?).");
            }

            var json = new JObject
            {
                ["hit"] = result.Hit,
                ["entityId"] = result.SceneEntityId,
                ["crdtEntityId"] = result.CrdtEntityId,
            };

            if (result.FailureReason != null)
                json["reason"] = result.FailureReason;

            if (result.Hit)
            {
                json["hitPoint"] = McpJson.Vector(result.HitPoint);
                json["distance"] = Math.Round(result.Distance, 2);
            }

            if (result.HoverText != null)
                json["hoverText"] = result.HoverText;

            if (result.BlockedByEntityId != null)
            {
                json["blockedByEntityId"] = result.BlockedByEntityId;
                json["blockedByCrdtId"] = result.BlockedByCrdtId;
                json["blockedByCollider"] = result.BlockedByColliderName;
            }

            if (result.UpRayMissed)
                json["upRayMissed"] = true;

            return McpToolResult.Text(json.ToString(Formatting.Indented));
        }
    }
}
