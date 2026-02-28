using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    public class JavaScriptExecutionException : Exception
    {
        public string ErrorDetails => Message;

        public JavaScriptExecutionException(string message) : base(message) { }

        public JavaScriptExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
