namespace DCL.Multiplayer.Connections.Typing
{
    public struct LightResult<T>
    {
        public static readonly LightResult<T> FAILURE = new ();

        public readonly T Result;
        public bool Success;

        public LightResult(T result, bool success)
        {
            this.Result = result;
            this.Success = success;
        }
    }
}
