using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for the hint label
    /// </summary>
    public class DebugHintDef : IDebugElementDef
    {
        /// <summary>
        ///     Based on the position value Builder will place the hint above or below the element
        /// </summary>
        public enum Position
        {
            /// <summary>
            ///     Below the element
            /// </summary>
            Before,

            /// <summary>
            ///     Above the element
            /// </summary>
            After,
        }

        /// <summary>
        ///     The visual style of the hint depends on the kind
        /// </summary>
        public enum Kind
        {
            /// <summary>
            ///     Light and small hint
            /// </summary>
            Info,

            /// <summary>
            ///     Yellow with warning sign
            /// </summary>
            Warning,

            /// <summary>
            ///     Red with error sign
            /// </summary>
            Error,
        }

        public readonly string? Text;
        public readonly ElementBinding<string>? Binding;

        public readonly Position HintPosition;
        public readonly Kind HintKind;

        /// <summary>
        ///     Create the definition without a binding
        /// </summary>
        /// <param name="text"></param>
        /// <param name="hintPosition"></param>
        /// <param name="hintKind"></param>
        public DebugHintDef(string text, Position hintPosition = Position.Before, Kind hintKind = Kind.Info)
        {
            Text = text;
            HintPosition = hintPosition;
            HintKind = hintKind;
            Binding = null;
        }

        /// <summary>
        ///     Create the definition with a binding
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="hintPosition"></param>
        /// <param name="hintKind"></param>
        public DebugHintDef(ElementBinding<string> binding, Position hintPosition = Position.Before, Kind hintKind = Kind.Info)
        {
            Text = null;
            HintPosition = hintPosition;
            HintKind = hintKind;
            Binding = binding;
        }
    }
}
