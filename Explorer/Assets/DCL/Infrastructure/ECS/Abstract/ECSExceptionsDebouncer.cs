using DCL.Diagnostics;
using System;

namespace ECS.Abstract
{
    public class ECSExceptionsDebouncer : ProgressiveWindowDebouncer
    {
        public static readonly ECSExceptionsDebouncer INSTANCE = new ();

        private ECSExceptionsDebouncer() : base(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1), backoffFactor: 1.6) { }
    }
}
