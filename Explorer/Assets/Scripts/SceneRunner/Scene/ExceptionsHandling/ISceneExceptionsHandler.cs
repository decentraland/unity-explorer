using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    public interface ISceneExceptionsHandler : ISystemGroupExceptionHandler, IJavaScriptErrorsHandler, IDisposable
    {
        void OnEngineException(Exception exception, string category = ReportCategory.ENGINE);
    }
}
