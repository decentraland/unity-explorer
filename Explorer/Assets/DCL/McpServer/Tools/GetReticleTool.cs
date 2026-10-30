using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.SyntheticInput;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>Reads what the reticle ray reaches right now, the way a human finds a target by aiming.</summary>
    public class GetReticleTool : McpTool
    {
        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "get_reticle";

        public override string Description =>
            "What is under the reticle right now: the entity the camera ray reaches (scene entity with its crdtEntityId, hover text "
            + "and whether a press would be in range of its PointerEvents; an avatar; bare world geometry; or nothing), with the ray "
            + "and the distances. The ray goes through the screen centre while the cursor is locked and through the cursor when it is "
            + "free, exactly as for a human — so this is the one-call check for 'can a player find this by aiming'. Read-only: no "
            + "hover or press reaches the scene. Aim first with look_at or camera_look.";

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
               .Boolean("available", "False while the pipeline builds no ray (the cursor is panning, or the in-world camera is open).")
               .String("reason", "Why no reading is available.", nullable: true)
               .Enum<ReticleTargetKind>("kind", "What the ray reached.")
               .Boolean("rayFromScreenCenter", "True while the cursor is captured; false when the ray goes through the free cursor.")
               .Boolean("cursorLocked")
               .Object("ray", McpJsonSchema.Object().Object("origin", Vector3Schema()).Object("direction", Vector3Schema()), "World-space ray.")
               .Object("cursor", McpJsonSchema.Object().Number("x").Number("y").Object("center", McpJsonSchema.Object().Number("x").Number("y")),
                    "Where the ray enters the screen: image pixels (origin top-left) in the 'screen' space, plus the normalized center.")
               .Object("screen", McpJsonSchema.Object().Integer("width").Integer("height"), "The screen size the cursor pixel is in.")
               .String("colliderName", nullable: true)
               .Object("hitPoint", Vector3Schema(), "World point the ray hit.", nullable: true)
               .Number("distanceToCamera", "Ray length to the hit.", nullable: true)
               .Integer("globalEntityId", "For an avatar: the engine entity id in the global world.", nullable: true)
               .Integer("entityId", "For a scene entity: the engine entity id in its scene world.", nullable: true)
               .Integer("crdtEntityId", "For a scene entity: the SDK entity id.", nullable: true)
               .String("sceneId", "The scene the entity belongs to; null when it is not the current scene.", nullable: true)
               .String("sceneName", nullable: true)
               .Number("distanceToPlayer", "Avatar-to-hit distance, the one PointerEvents maxDistance gates on.", nullable: true)
               .Boolean("hasPointerEvents", "Whether the entity declares any PointerEvents.")
               .Boolean("withinRange", "Whether some declared entry qualifies at the current distances, so a press would reach the entity.")
               .Boolean("hoverQualified", "Whether the pipeline holds the entity hovered and in range this frame (its hover feedback is shown).")
               .String("hoverText", "The hover text a human would see, null when none.", nullable: true)
               .ObjectArray("tooltips", McpJsonSchema.Object().String("text"), "The tooltip lines shown for the hovered entity.")
               .Build();

        private static McpJsonSchema Vector3Schema() =>
            McpJsonSchema.Object().Number("x").Number("y").Number("z");

        public GetReticleTool(SyntheticInputAgent syntheticInput)
        {
            this.syntheticInput = syntheticInput;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            ReticleReading reading = syntheticInput.ReadReticle();

            if (!reading.Available)
                return UniTask.FromResult(McpToolResult.Error(reading.UnavailableReason ?? "the reticle is not readable right now"));

            JObject json = reading.ToJson();
            json["screen"] = UiDiscovery.ScreenJson();

            Vector2 normalized = UiScreenGeometry.NormalizedCenterOf(new Rect(reading.PointerImagePoint, Vector2.zero));

            json["cursor"] = new JObject
            {
                ["x"] = Math.Round(reading.PointerImagePoint.x, 1),
                ["y"] = Math.Round(reading.PointerImagePoint.y, 1),
                ["center"] = new JObject { ["x"] = Math.Round(normalized.x, 4), ["y"] = Math.Round(normalized.y, 4) },
            };

            return UniTask.FromResult(McpToolResult.TextWithStructured(Summarize(in reading), json));
        }

        private static string Summarize(in ReticleReading reading) =>
            reading.Kind switch
            {
                ReticleTargetKind.SceneEntity =>
                    $"reticle on scene entity crdtId {reading.CrdtEntityId} (engine id {reading.SceneEntityId}), {reading.DistanceToCamera:F1}m from the camera, "
                    + $"{(reading.WithinRange ? "within" : "out of")} PointerEvents range, hover text: {reading.HoverText ?? "none"}",
                ReticleTargetKind.Avatar => $"reticle on an avatar (global entity {reading.GlobalEntityId}), {reading.DistanceToCamera:F1}m from the camera",
                ReticleTargetKind.World => $"reticle on non-entity geometry ('{reading.ColliderName}'), {reading.DistanceToCamera:F1}m from the camera",
                _ => "reticle on nothing (the ray hit no collider within reach)",
            };
    }
}
