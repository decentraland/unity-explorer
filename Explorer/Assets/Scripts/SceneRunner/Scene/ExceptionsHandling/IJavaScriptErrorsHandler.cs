namespace SceneRunner.Scene.ExceptionsHandling
{
    public interface IJavaScriptErrorsHandler
    {
        void OnJavaScriptException(string message);
    }
}
