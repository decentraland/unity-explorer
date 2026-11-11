using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.SyntheticInput
{
    /// <summary>What the reticle ray reached on the current frame.</summary>
    public enum ReticleTargetKind : byte
    {
        Nothing,
        SceneEntity,
        Avatar,

        /// <summary>Geometry that belongs to no entity: terrain, the client's own colliders.</summary>
        World,
    }

    /// <summary>Reads the current frame's reticle state; installed by the systems that can see the interaction pipeline.</summary>
    public delegate ReticleReading ReticleProbe();

    /// <summary>
    ///     A read of what sits under the reticle right now: the pipeline's own raycast result, so what a human would
    ///     find by aiming. Produces no scene event.
    /// </summary>
    public struct ReticleReading
    {
        public bool Available;
        public string? UnavailableReason;

        public ReticleTargetKind Kind;

        /// <summary>The ray goes through the screen centre while the cursor is captured; through the cursor when it is free.</summary>
        public bool RayFromScreenCenter;
        public bool CursorLocked;

        /// <summary>Image pixels, origin top-left.</summary>
        public Vector2 PointerImagePoint;

        public Ray Ray;

        public int SceneEntityId;
        public int CrdtEntityId;
        public string? SceneId;
        public string? SceneName;
        public int GlobalEntityId;

        public string? ColliderName;
        public Vector3 HitPoint;
        public float DistanceToCamera;
        public float? DistanceToPlayer;

        public bool HasPointerEvents;

        /// <summary>Some declared entry qualifies at the current distances, so a press would reach the entity.</summary>
        public bool WithinRange;

        /// <summary>The pipeline holds the entity hovered and in range this frame (its hover feedback is shown).</summary>
        public bool HoverQualified;

        public string? HoverText;
        public string[] Tooltips;

        public static ReticleReading Unavailable(string reason) =>
            new () { Available = false, UnavailableReason = reason, SceneEntityId = -1, GlobalEntityId = -1, Tooltips = Array.Empty<string>() };

        public static ReticleReading Empty() =>
            new () { Available = true, Kind = ReticleTargetKind.Nothing, SceneEntityId = -1, GlobalEntityId = -1, Tooltips = Array.Empty<string>() };

        /// <summary>
        ///     The wire shape shared by the MCP tool and the AltTester probe. Every key is always present (null where
        ///     the kind has no value for it), so a schema can describe the payload exactly.
        /// </summary>
        public readonly JObject ToJson()
        {
            bool sceneEntity = Kind == ReticleTargetKind.SceneEntity;
            bool anyHit = Kind != ReticleTargetKind.Nothing;

            var tooltips = new JArray();

            foreach (string tooltip in Tooltips)
                tooltips.Add(new JObject { ["text"] = tooltip });

            return new JObject
            {
                ["available"] = Available,
                ["reason"] = Available ? null : UnavailableReason,
                ["kind"] = KindName(Kind),
                ["rayFromScreenCenter"] = RayFromScreenCenter,
                ["cursorLocked"] = CursorLocked,
                ["ray"] = new JObject
                {
                    ["origin"] = VectorJson(Ray.origin),
                    ["direction"] = VectorJson(Ray.direction),
                },
                ["colliderName"] = anyHit ? ColliderName : null,
                ["hitPoint"] = anyHit ? VectorJson(HitPoint) : null,
                ["distanceToCamera"] = anyHit ? Math.Round(DistanceToCamera, 2) : null,
                ["globalEntityId"] = Kind == ReticleTargetKind.Avatar ? GlobalEntityId : null,
                ["entityId"] = sceneEntity ? SceneEntityId : null,
                ["crdtEntityId"] = sceneEntity ? CrdtEntityId : null,
                ["sceneId"] = sceneEntity ? SceneId : null,
                ["sceneName"] = sceneEntity ? SceneName : null,
                ["distanceToPlayer"] = sceneEntity && DistanceToPlayer != null ? Math.Round(DistanceToPlayer.Value, 2) : null,
                ["hasPointerEvents"] = sceneEntity && HasPointerEvents,
                ["withinRange"] = sceneEntity && WithinRange,
                ["hoverQualified"] = sceneEntity && HoverQualified,
                ["hoverText"] = sceneEntity ? HoverText : null,
                ["tooltips"] = tooltips,
            };
        }

        public static string KindName(ReticleTargetKind kind) =>
            kind switch
            {
                ReticleTargetKind.SceneEntity => "scene_entity",
                ReticleTargetKind.Avatar => "avatar",
                ReticleTargetKind.World => "world",
                _ => "nothing",
            };

        private static JObject VectorJson(Vector3 vector) =>
            new ()
            {
                ["x"] = Math.Round(vector.x, 3),
                ["y"] = Math.Round(vector.y, 3),
                ["z"] = Math.Round(vector.z, 3),
            };
    }
}
