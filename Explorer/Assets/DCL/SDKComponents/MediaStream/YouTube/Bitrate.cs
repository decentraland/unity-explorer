namespace DCL.SDKComponents.MediaStream.YouTube
{
    public readonly struct Bitrate
    {
        public long BitsPerSecond { get; }

        public Bitrate(long bitsPerSecond)
        {
            BitsPerSecond = bitsPerSecond;
        }

        public override string ToString() => $"{BitsPerSecond} bit/s";
    }
}
