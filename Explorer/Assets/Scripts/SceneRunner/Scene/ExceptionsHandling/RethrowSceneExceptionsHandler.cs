using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using Microsoft.ClearScript;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     A dummy version of <see cref="ISceneExceptionsHandler" /> that rethrows all exceptions
    /// </summary>
    public class RethrowSceneExceptionsHandler : ISceneExceptionsHandler
    {
        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType) =>
            throw exception;

        public void OnJavaScriptException(string message)
        {
            throw new ScriptEngineException(message);
        }

        public void Dispose() { }

        public void OnEngineException(Exception exception, string category = ReportCategory.ENGINE)
        {
            throw exception;
        }
    }
}
