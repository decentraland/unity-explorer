using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Presses a pointer button on a scene entity by composing single-event <see cref="McpEcsPointerEventIntent" />s
    ///     delivered by McpPointerEventSystem, which validates the aim with the same raycast rules as the reticle
    ///     pipeline before filling the entity's pointer-event intent like a real click. A full click is a press
    ///     followed by a release ordered onto a later scene tick, merged into one result here.
    /// </summary>
    public class ClickEntityTool : McpTool
    {
        /// <summary>Wire-facing subset of <see cref="InputAction" />: only the three pointer buttons make sense for a click.</summary>
        private enum PointerButton : byte
        {
            POINTER,
            PRIMARY,
            SECONDARY,
        }

        /// <summary>Wire-facing gesture kinds: a full click, or a single press/release leg.</summary>
        private enum ClickKind : byte
        {
            /// <summary>Pointer down, then pointer up on the next scene tick.</summary>
            CLICK,
            DOWN,
            UP,
        }

        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        private readonly World world;
        private readonly Entity playerEntity;

        public override string Name => "click_entity";

        public override string Description =>
            "Press and release a pointer button on a scene entity so its PointerEvents fire exactly like a real click. "
            + "The aim is validated by a physics raycast from the camera: occluders and the entity's maxDistance apply, and a miss "
            + "returns hit:false with the blocking entity. Ids come from list_scene_entities. For entities whose collider "
            + "sits away from their pivot (e.g. GLTF meshes), pass an explicit x/y/z world point to aim at.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("entityId", "Target entity id in the current scene world (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target.")
                  .Number("x", "World-space aim point; overrides the automatic aim at the entity's collider center.")
                  .Number("y")
                  .Number("z")
                  .Enum<PointerButton>("button", "Which input action to press. Default pointer (left click / IA_POINTER).")
                  .Enum<ClickKind>("eventType", "click = down, then up on the next scene tick. Default click.")
                  .Number("timeoutSec", "Seconds to wait for delivery. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ClickEntityTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool hasEntityId = arguments.TryGetInt("entityId", out int entityId);

            bool hasAimPoint = arguments.TryGetFloat("x", out float x)
                               & arguments.TryGetFloat("y", out float y)
                               & arguments.TryGetFloat("z", out float z);

            if (!hasEntityId && !hasAimPoint)
                return McpToolResult.Error("Provide entityId, or a full x/y/z world aim point, or both.");

            if (!arguments.TryGetEnum("button", PointerButton.POINTER, out PointerButton pointerButton))
                return McpToolResult.Error("button must be one of: pointer, primary, secondary.");

            InputAction button = pointerButton switch
                                 {
                                     PointerButton.PRIMARY => InputAction.IaPrimary,
                                     PointerButton.SECONDARY => InputAction.IaSecondary,
                                     _ => InputAction.IaPointer,
                                 };

            if (!arguments.TryGetEnum("eventType", ClickKind.CLICK, out ClickKind kind))
                return McpToolResult.Error("eventType must be one of: click, down, up.");

            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            int targetEntityId = hasEntityId ? entityId : -1;
            Vector3? aimPoint = hasAimPoint ? new Vector3(x, y, z) : null;

            McpPointerClickResult result;

            try
            {
                // A single budget for the whole gesture: it covers both a paused simulation that never runs
                // the system and a release stuck waiting for the scene tick to advance.
                result = await RunGestureAsync(targetEntityId, aimPoint, button, kind)
                              .AttachExternalCancellation(ct)
                              .Timeout(TimeSpan.FromSeconds(timeoutSec));
            }
            catch (TimeoutException)
            {
                await McpRequest.AbandonAsync<McpEcsPointerEventIntent>(world, playerEntity);
                return McpToolResult.Error($"click_entity did not complete within {timeoutSec}s (is the simulation paused?).");
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
                json["hitPoint"] = result.HitPoint.ToVector();
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

            return McpToolResult.Json(json);
        }

        /// <summary>
        ///     Composes the requested gesture from single-event intents: a lone press or release is one delivery;
        ///     a click is a press followed by a release that carries the press handoff so the system keeps it
        ///     ordered onto a later scene tick.
        /// </summary>
        private async UniTask<McpPointerClickResult> RunGestureAsync(int targetEntityId, Vector3? aimPoint, InputAction button, ClickKind kind)
        {
            PointerEventType pressType = kind == ClickKind.UP ? PointerEventType.PetUp : PointerEventType.PetDown;

            McpPointerClickResult down = await SendAsync(new McpEcsPointerEventIntent(targetEntityId, aimPoint, button, pressType));

            if (kind != ClickKind.CLICK || !down.Hit)
                return down;

            McpPointerClickResult up = await SendAsync(new McpEcsPointerEventIntent(targetEntityId, aimPoint, button, PointerEventType.PetUp, down.Press));

            if (!up.UpRayMissed)
                return up; // a fresh release, or a failure (scene reload, timeout, preemption) that tells the whole story

            // The release needed the press-frame hit: report the press delivery and flag the divergence.
            down.UpRayMissed = true;

            if (!up.Hit)
                down.FailureReason = $"the entity disappeared after the press ({up.FailureReason}); only PetDown was delivered";

            return down;
        }

        private UniTask<McpPointerClickResult> SendAsync(McpEcsPointerEventIntent request) =>
            McpRequest.SendAsync(world, playerEntity, request, new McpPointerClickResult
            {
                Hit = false,
                FailureReason = "preempted by a newer click_entity call",
            });
    }
}
