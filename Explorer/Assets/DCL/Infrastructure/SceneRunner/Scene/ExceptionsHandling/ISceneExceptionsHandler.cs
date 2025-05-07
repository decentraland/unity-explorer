using Arch.SystemGroups;
using DCL.Diagnostics;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    public interface ISceneExceptionsHandler : ISystemGroupExceptionHandler, IJavaScriptErrorsHandler, IJavaScriptApiExceptionsHandler, IDisposable
    {
        void OnEngineException(Exception exception, string category = ReportCategory.ENGINE);
    }
}
