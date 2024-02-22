using System;

namespace DCL.Multiplayer.Connections.Typing
{
    public readonly struct LightResult<T>
    {
        public static readonly LightResult<T> FAILURE = new ();

        public readonly T Result;
        public readonly bool Success;

        public LightResult(T result)
        {
            Result = result;
            Success = true;
        }

        public override string ToString() =>
            Success
                ? $"Result is success: {Result}"
                : "Result is failure";
    }

    public static class LightResultExtensions
    {
        public static LightResult<T> AsSuccess<T>(this T result) =>
            new (result);

        public static void EnsureSuccess<T>(this LightResult<T> result, string errorMessage)
        {
            if (result.Success == false)
                throw new Exception($"Result is failure: {errorMessage}");
        }
    }
}
