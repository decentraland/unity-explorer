namespace DCL.DebugUtilities.UIBindings
{
    /// <summary>
    ///     Snapshot of a rolling time-series for the line chart element. The same backing array is
    ///     reused frame-to-frame; only <see cref="WriteIndex" />, <see cref="Count" />, and
    ///     <see cref="DisplayValue" /> change, so propagating an update does not allocate.
    /// </summary>
    public readonly struct LineChartBuffer
    {
        public readonly float[]? Buffer;
        public readonly int WriteIndex;
        public readonly int Count;
        public readonly float DisplayValue;

        public LineChartBuffer(float[]? buffer, int writeIndex, int count, float displayValue)
        {
            Buffer = buffer;
            WriteIndex = writeIndex;
            Count = count;
            DisplayValue = displayValue;
        }
    }
}
