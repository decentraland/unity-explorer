namespace SceneRunner.Scene.ExceptionsHandling
{
    public interface IJavaScriptErrorsHandler
    {
        /// <summary>
        ///     TODO Find a way to propagate Exception from JS to C#,
        ///     <see cref="ScriptItem.ThrowLastScriptError" /> that is static and not exposed for some reason,
        ///     something similar would be desirable
        /// </summary>
        /// <param name="message"></param>
        void OnJavaScriptException(string message);
    }
}
