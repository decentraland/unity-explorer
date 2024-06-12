using Cysharp.Threading.Tasks;
using System;

namespace DCL.Utilities.Extensions
{
    public struct SuppressExceptionWithFallback
    {
        [Flags]
        public enum Behaviour
        {
            Default = 0,
            /// <summary>
            ///     It might be undesirable to ignore Cancellation as it's controlled from the consumer flow
            /// </summary>
            SuppressCancellation = 1 << 0,

            /// <summary>
            ///     By default suppresses <see cref="UnityWebRequestException" /> only
            /// </summary>
            SuppressAnyException = 1 << 1,
        }
    }
}
