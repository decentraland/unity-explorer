namespace DCL.SDKComponents.MediaStream.YouTube
{
    public readonly struct VideoResolution
    {
        public int Width { get; }
        public int Height { get; }

        public VideoResolution(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Width}x{Height}";
    }
}
