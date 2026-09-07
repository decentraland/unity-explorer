namespace DCL.VideoPlayback
{
    // The consumer only reads Count, using "buffered range exists" as its
    // readiness gate before applying playback properties.
    public sealed class TimeRanges
    {
        public int Count { get; }

        public TimeRanges(int count)
        {
            Count = count;
        }
    }
}
