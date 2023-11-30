using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    public interface IJavaScriptErrorsHandler
    {
        void OnJavaScriptException(Exception exception);
    }
}
