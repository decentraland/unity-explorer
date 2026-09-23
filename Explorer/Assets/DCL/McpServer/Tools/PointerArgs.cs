using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>Wire-facing subset of <see cref="InputAction" />: the three pointer buttons a click or a hold can use.</summary>
    public enum PointerButton : byte
    {
        Pointer,
        Primary,
        Secondary,
    }

    /// <summary>Argument parsing shared by the tools that aim a pointer gesture at the world: the button and the aim.</summary>
    internal static class PointerArgs
    {
        public const string BUTTON_DESCRIPTION = "Which input action to press. Default pointer (left click / IA_POINTER).";

        public const float DEFAULT_TIMEOUT_SEC = 3f;
        public const float MIN_TIMEOUT_SEC = 0.5f;
        public const float MAX_TIMEOUT_SEC = 15f;

        private const string TARGETED_ENTITY_DESCRIPTION = "Target entity id in the current scene world (from list_scene_entities). Omit only when x/y/z are given, then the ray decides the target.";
        private const string BROADCAST_ENTITY_DESCRIPTION = "Aim the reticle at this entity (from list_scene_entities) so the action lands entity-bound on it. Omit for a scene-root broadcast.";
        private const string TARGETED_AIM_POINT_DESCRIPTION = "World-space aim point; overrides the automatic aim at the entity's collider center.";
        private const string BROADCAST_AIM_POINT_DESCRIPTION = "World-space aim point; an alternative to entityId (and it overrides the aim at the entity's collider center).";

        /// <summary>Declares the aim arguments <see cref="TryParseAim" /> reads: entityId, x/y/z and sceneId.</summary>
        public static McpJsonSchema DescribeAim(McpJsonSchema schema, string gesture, bool requireTarget = true) =>
            DescribeSceneId(schema.Integer("entityId", requireTarget ? TARGETED_ENTITY_DESCRIPTION : BROADCAST_ENTITY_DESCRIPTION)
                                  .Number("x", requireTarget ? TARGETED_AIM_POINT_DESCRIPTION : BROADCAST_AIM_POINT_DESCRIPTION)
                                  .Number("y")
                                  .Number("z"), gesture);

        /// <summary>Declares the sceneId argument that pins a gesture to one scene.</summary>
        public static McpJsonSchema DescribeSceneId(McpJsonSchema schema, string gesture) =>
            schema.String("sceneId", $"Pin the {gesture} to this scene (id from get_scene_state): it fails instead of landing in another scene if the player moved.");

        /// <summary>The timeoutSec argument, clamped to the range every pointer gesture accepts.</summary>
        public static float ClampTimeout(JObject arguments) =>
            Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

        public static bool TryGetButton(JObject arguments, out InputAction button, [NotNullWhen(false)] out string? error)
        {
            button = InputAction.IaPointer;
            error = null;

            if (!arguments.TryGetEnum("button", PointerButton.Pointer, out PointerButton wireButton))
            {
                error = arguments.EnumArgumentError<PointerButton>("button");
                return false;
            }

            button = wireButton switch
                     {
                         PointerButton.Primary => InputAction.IaPrimary,
                         PointerButton.Secondary => InputAction.IaSecondary,
                         _ => InputAction.IaPointer,
                     };

            return true;
        }

        /// <summary>
        ///     A partly readable x/y/z is refused rather than ignored: the gesture would otherwise aim at nothing,
        ///     or at the entity's center, instead of at the point the caller sent.
        /// </summary>
        public static bool TryParseAim(JObject arguments, bool requireTarget, out PointerAim aim, [NotNullWhen(false)] out string? error)
        {
            aim = PointerAim.None;
            error = null;

            bool hasEntityId = arguments.TryGetInt("entityId", out int entityId);

            bool hasAimPoint = arguments.TryGetFloat("x", out float x)
                               & arguments.TryGetFloat("y", out float y)
                               & arguments.TryGetFloat("z", out float z);

            if (!hasAimPoint && (arguments["x"] != null || arguments["y"] != null || arguments["z"] != null))
            {
                error = "x, y and z must all be numbers to aim at a world point"
                        + (requireTarget ? "." : "; omit all three for a scene-root broadcast.")
                        + arguments.NonNumericHint("x", "y", "z");

                return false;
            }

            if (requireTarget && !hasEntityId && !hasAimPoint)
            {
                error = "Provide entityId, or a full x/y/z world aim point, or both." + arguments.NonNumericHint("entityId", "x", "y", "z");
                return false;
            }

            aim = new PointerAim(hasEntityId ? entityId : null, arguments.GetStringOrNull("sceneId"), hasAimPoint ? new Vector3(x, y, z) : null);
            return true;
        }
    }
}
