namespace Utility
{
    public static class BitWiseUtils
    {
        /// <summary>
        ///     Returns true if the bit was not set before
        /// </summary>
        public static bool TrySetBit(ref long value, int bitIndex)
        {
            long mask = 1L << bitIndex;
            bool result = (value & mask) == 0;
            value |= mask;
            return result;
        }
    }
}
