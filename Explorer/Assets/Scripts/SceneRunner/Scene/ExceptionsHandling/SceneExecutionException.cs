using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     Exception that suspended scene
    /// </summary>
    public class SceneExecutionException : Exception
    {
        public SceneExecutionException(Exception inner) : base(null, inner) { }
    }
}
