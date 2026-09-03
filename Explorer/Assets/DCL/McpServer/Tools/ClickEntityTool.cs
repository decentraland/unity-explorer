using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Presses a pointer button on a scene entity via <see cref="SyntheticInputAgent" />, which delivers the
    ///     gesture through the real reticle pipeline (a synthetic aim and button edge posted to it), so occlusion,
    ///     distance gates and the scene write-back are the production ones. A full click is a press followed by a
    ///     release ordered onto a later scene tick, merged into one result.
    /// </summary>
    public class ClickEntityTool : McpTool
    {
        /// <summary>Wire-facing subset of <see cref="InputAction" />: only the three pointer buttons make sense for a click.</summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum PointerButton : byte
        {
            POINTER,
            PRIMARY,
            SECONDARY,
        }

        /// <summary>
        ///     Wire-facing gesture kinds: a full click, or a single press/release leg. The member names ARE the
        ///     wire contract — McpWireEnum derives each argument value from them — so they stay SCREAMING_CASE,
        ///     as in every other tool's wire enum.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum ClickKind : byte
        {
            /// <summary>Pointer down, then pointer up on the next scene tick.</summary>
            CLICK,

            /// <summary>Press-only leg; parsed from the wire and exposed via the schema through reflection over this enum.</summary>
            [UsedImplicitly]
            DOWN,
            UP,
        }

        private const float DEFAULT_TIMEOUT_SEC = 3f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;

        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "click_entity";

        public override string Description =>
            "Press and release a pointer button on a scene entity so its PointerEvents fire exactly like a real click. "
            + "The click runs through the real reticle pipeline: occluders and the entity's maxDistance apply, and a miss "
            + "returns hit:false with the blocking entity. Ids come from list_scene_entities. For entities whose collider "
            + "sits away from their pivot (e.g. GLTF meshes), pass an explicit x/y/z world point to aim at.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("entityId", "Target entity id in the current scene world (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target.")
                  .Number("x", "World-space aim point; overrides the automatic aim at the entity's collider center.")
                  .Number("y")
                  .Number("z")
                  .String("sceneId", "Pin the click to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.")
                  .Enum<PointerButton>("button", "Which input action to press. Default pointer (left click / IA_POINTER).")
                  .Enum<ClickKind>("eventType", "click = down, then up on the next scene tick. Default click.")
                  .Number("timeoutSec", "Seconds to wait for delivery. Default 3, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ClickEntityTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            bool hasEntityId = arguments.TryGetInt("entityId", out int entityId);

            bool hasAimPoint = arguments.TryGetFloat("x", out float x)
                               & arguments.TryGetFloat("y", out float y)
                               & arguments.TryGetFloat("z", out float z);

            if (!hasEntityId && !hasAimPoint)
                return McpToolResult.Error("Provide entityId, or a full x/y/z world aim point, or both." + arguments.NonNumericHint("entityId", "x", "y", "z"));

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
            string? sceneId = arguments["sceneId"]?.Type == JTokenType.String ? arguments["sceneId"]!.Value<string>() : null;
            Vector3? aimPoint = hasAimPoint ? new Vector3(x, y, z) : null;

            SyntheticPointerResult result = kind switch
                                            {
                                                ClickKind.DOWN => await syntheticInput.PointerDownAsync(targetEntityId, sceneId, aimPoint, screenPoint: null, button, timeoutSec, ct),
                                                ClickKind.UP => await syntheticInput.PointerUpAsync(targetEntityId, sceneId, aimPoint, screenPoint: null, button, timeoutSec, ct),
                                                _ => await syntheticInput.ClickAsync(targetEntityId, sceneId, aimPoint, screenPoint: null, button, timeoutSec, ct),
                                            };

            if (result.TimedOut)
                return McpToolResult.Error($"click_entity did not complete within {timeoutSec}s (is the simulation paused?).");

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
    }
}
