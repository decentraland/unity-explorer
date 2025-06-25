using DCL.Diagnostics;
using System;

namespace ECS.Abstract
{
    /// <summary>
    ///     Refer to https://github.com/decentraland/unity-explorer/pull/4558 for more details.
    /// </summary>
    public class ECSExceptionsDebouncer : ProgressiveWindowDebouncer
    {
        public static readonly ECSExceptionsDebouncer INSTANCE = new ();

        private ECSExceptionsDebouncer() : base(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1), backoffFactor: 1.6) { }
    }
}
