// Decentraland.Networking.Bitwise — Quantize
// Copy this file into your project alongside generated *.Bitwise.cs files.
// ReSharper disable once RedundantUsingDirective

using System;

namespace Decentraland.Networking.Bitwise
    // ReSharper disable once ArrangeNamespaceBody
{
    /// <summary>
    ///     Static helpers for quantizing float values to/from unsigned integers.
    ///     Intended for use with protobuf uint32 fields: the integer is transmitted
    ///     as a protobuf varint, while the float accessor lives in a generated partial class.
    /// </summary>
    public static class Quantize
    {
        /// <summary>
        ///     Encodes <paramref name="value" /> to a quantized <see cref="uint" />.
        ///     Values outside [<paramref name="min" />, <paramref name="max" />] are clamped.
        /// </summary>
        public static uint Encode(float value, float min, float max, int bits)
        {
            var steps = (1u << bits) - 1;
            var t = Math.Clamp((value - min) / (max - min), 0f, 1f);
            return (uint)MathF.Round(t * steps);
        }

        /// <summary>Decodes a quantized <see cref="uint" /> back to a float.</summary>
        public static float Decode(uint encoded, float min, float max, int bits)
        {
            var steps = (1u << bits) - 1;
            return (float)encoded / steps * (max - min) + min;
        }
    }
}
