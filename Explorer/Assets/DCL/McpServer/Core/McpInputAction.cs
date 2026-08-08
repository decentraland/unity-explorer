using DCL.ECSComponents;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Wire-facing spelling of <see cref="InputAction" />, so a tool's schema and the parsing of its argument
    ///     derive from one enum (see <see cref="McpWireEnum{T}" />): "pointer" ↔ IA_POINTER, "action_5" ↔
    ///     IA_ACTION_5. It mirrors <see cref="InputAction" /> member for member — all of which the production
    ///     input map in GlobalInteractionPlugin binds, so a scene may read any of them; a tool that accepts only
    ///     a subset narrows its schema through the allowed parameter of <see cref="McpJsonSchema.Enum{T}" />
    ///     rather than declaring an enum of its own.
    ///     <para>
    ///         Members are declared in the protobuf's own order, so each lands on its counterpart's value and
    ///         <see cref="McpInputActionExtensions" /> converts by cast instead of a mapping table that could
    ///         drift. Nothing in the language enforces that alignment — PressInputActionToolShould does, by
    ///         checking every member against the protobuf member its name spells.
    ///     </para>
    /// </summary>
    public enum McpInputAction : byte
    {
        POINTER,
        PRIMARY,
        SECONDARY,
        ANY,
        FORWARD,
        BACKWARD,
        RIGHT,
        LEFT,
        JUMP,
        WALK,
        ACTION_3,
        ACTION_4,
        ACTION_5,
        ACTION_6,
        MODIFIER,
    }

    public static class McpInputActionExtensions
    {
        /// <summary>Valid by construction: the members are declared with their protobuf values.</summary>
        public static InputAction ToInputAction(this McpInputAction action) =>
            (InputAction)action;
    }
}
