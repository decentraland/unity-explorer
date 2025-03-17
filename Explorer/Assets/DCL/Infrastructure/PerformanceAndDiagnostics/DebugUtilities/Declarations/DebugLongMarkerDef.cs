using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition of the Label on which a long value in certain units is reflected
    /// </summary>
    public class DebugLongMarkerDef : IDebugElementDef
    {
        public enum Unit
        {
            /// <summary>
            ///     Time in nanoseconds as it's non-divisible
            /// </summary>
            TimeNanoseconds,

            /// <summary>
            ///     Bits
            /// </summary>
            Bits,

            /// <summary>
            ///     Bytes
            /// </summary>
            Bytes,

            /// <summary>
            ///     Raw Value is formatted as is
            /// </summary>
            NoFormat,
        }

        /// <summary>
        ///     One-way binding from a long value to a label
        /// </summary>
        public readonly ElementBinding<ulong> Binding;

        public readonly Unit MarkerUnit;

        public DebugLongMarkerDef(ElementBinding<ulong> binding, Unit markerUnit)
        {
            Binding = binding;
            MarkerUnit = markerUnit;
        }
    }
}
