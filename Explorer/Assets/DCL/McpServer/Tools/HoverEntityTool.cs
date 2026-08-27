using Cysharp.Threading.Tasks;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Aims the reticle at a scene entity (or an explicit world point) and holds the hover without pressing
    ///     anything, via <see cref="SyntheticInputAgent.HoverAsync" />: the scene observes the same
    ///     PetHoverEnter/PetHoverLeave flow a real cursor produces.
    /// </summary>
    public class HoverEntityTool : McpTool
    {
        private const float DEFAULT_SECONDS = 1f;
        private const float MIN_SECONDS = 0.1f;
        private const float MAX_SECONDS = 30f;

        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "hover_entity";

        public override string Description =>
            "Aim the reticle at a scene entity and hold the hover for a duration without clicking, so its hover "
            + "PointerEvents (PetHoverEnter/PetHoverLeave) fire exactly like a real cursor. Occluders and the entity's "
            + "maxDistance apply; the result reports what was hovered and its hover tooltip text. Ids come from "
            + "list_scene_entities. For entities whose collider sits away from their pivot, pass an explicit x/y/z world point.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("entityId", "Target entity id in the current scene world (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target.")
                  .Number("x", "World-space aim point; overrides the automatic aim at the entity's collider center.")
                  .Number("y")
                  .Number("z")
                  .String("sceneId", "Pin the hover to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.")
                  .Number("seconds", "How long to hold the hover. Default 1, max 30.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public HoverEntityTool(SyntheticInputAgent syntheticInput)
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
                return McpToolResult.Error("Provide entityId, or a full x/y/z world aim point, or both.");

            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", DEFAULT_SECONDS), MIN_SECONDS, MAX_SECONDS);

            int targetEntityId = hasEntityId ? entityId : -1;
            string? sceneId = arguments["sceneId"]?.Type == JTokenType.String ? arguments["sceneId"]!.Value<string>() : null;
            Vector3? aimPoint = hasAimPoint ? new Vector3(x, y, z) : null;

            SyntheticPointerResult result = await syntheticInput.HoverAsync(targetEntityId, sceneId, aimPoint, screenPoint: null, seconds, ct);

            if (result.TimedOut)
                return McpToolResult.Error($"hover_entity did not complete within {seconds + SyntheticInputAgent.COMPLETION_GRACE_SEC}s (is the simulation paused?).");

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

            return McpToolResult.Json(json);
        }
    }
}
