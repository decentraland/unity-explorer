using System;

namespace DCL.Multiplayer.Connections.Typing
{
    /// <summary>
    ///     If you create a <see cref="LightResult{T}" /> you must log an exception as the error is not preserved here
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
}
