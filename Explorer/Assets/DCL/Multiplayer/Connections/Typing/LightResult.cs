namespace DCL.Multiplayer.Connections.Typing
{
    public struct LightResult<T>
    {
        public static readonly LightResult<T> FAILURE = new ();

        public readonly T Result;
        public readonly bool Success;

        public LightResult(T result)
        {
            this.Result = result;
            this.Success = true;
        }
    }

    public static class LightResultExtensions
    {
        public static LightResult<T> AsSuccess<T>(this T result) =>
            new (result);
    }
}
