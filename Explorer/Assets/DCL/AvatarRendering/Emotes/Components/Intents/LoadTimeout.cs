namespace DCL.AvatarRendering.Emotes
{
    public struct LoadTimeout
    {
        public int Timeout { get; }

        public float ElapsedTime { get; private set; }

        public LoadTimeout(int timeout) : this()
        {
            Timeout = timeout;
        }

        public bool IsTimeout(float deltaTime)
        {
            ElapsedTime += deltaTime;
            return ElapsedTime >= Timeout;
        }
    }
}
