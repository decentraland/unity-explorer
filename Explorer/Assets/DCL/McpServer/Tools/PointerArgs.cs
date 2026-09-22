using DCL.ECSComponents;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
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

        public static bool TryGetButton(JObject arguments, out InputAction button, out string? error)
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
        public static bool TryParseAim(JObject arguments, bool requireTarget, out PointerAim aim, out string? error)
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
