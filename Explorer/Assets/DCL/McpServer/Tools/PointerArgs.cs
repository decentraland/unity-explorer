using DCL.ECSComponents;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Wire-facing subset of <see cref="InputAction" />: the three pointer buttons a click or a hold can use.
    ///     The member names ARE the wire contract (McpWireEnum derives each argument value from them).
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum PointerButton : byte
    {
        POINTER,
        PRIMARY,
        SECONDARY,
    }

    /// <summary>Argument parsing shared by the tools that aim a pointer gesture at the world: the button and the aim.</summary>
    internal static class PointerArgs
    {
        public const string BUTTON_DESCRIPTION = "Which input action to press. Default pointer (left click / IA_POINTER).";

        /// <summary>Reads the pointer button; a missing argument is the pointer (left) button.</summary>
        public static bool TryGetButton(JObject arguments, out InputAction button, out string? error)
        {
            button = InputAction.IaPointer;
            error = null;

            if (!arguments.TryGetEnum("button", PointerButton.POINTER, out PointerButton wireButton))
            {
                error = "button must be one of: pointer, primary, secondary.";
                return false;
            }

            button = wireButton switch
                     {
                         PointerButton.PRIMARY => InputAction.IaPrimary,
                         PointerButton.SECONDARY => InputAction.IaSecondary,
                         _ => InputAction.IaPointer,
                     };

            return true;
        }

        /// <summary>
        ///     Reads the aim: an entityId and/or a full x/y/z world point, plus the optional sceneId pin.
        ///     <paramref name="requireTarget" /> refuses an aimless call. An x/y/z that is only half readable is
        ///     refused in every case rather than dropped: the edge would otherwise be aimed at nothing (or at the
        ///     entity's center) while the caller reads the result as an aim at the point it sent.
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
