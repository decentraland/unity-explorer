namespace DCL.AvatarRendering.Emotes
{
    public readonly struct LoadTimeout
    {
        public int Timeout { get; }

        public float ElapsedTime { get; }
        public bool IsTimeout => ElapsedTime >= Timeout;

        public LoadTimeout(int timeout, float elapsedTime) : this()
        {
            Timeout = timeout;
            ElapsedTime = elapsedTime;
        }
    }
}
