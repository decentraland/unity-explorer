using DCL.Diagnostics;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     Debounces repeated scene script exceptions so a scene that throws every tick
    ///     produces a bounded number of reports instead of one per occurrence.
    ///     A dedicated instance keeps scene script fingerprints separate from the ECS exceptions tracker.
    /// </summary>
    public class SceneJsExceptionsDebouncer : ProgressiveWindowDebouncer
    {
        public static readonly SceneJsExceptionsDebouncer INSTANCE = new ();

        private SceneJsExceptionsDebouncer() : base(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1), backoffFactor: 1.6) { }
    }
}
