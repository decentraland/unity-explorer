using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     A dummy version of <see cref="ISceneExceptionsHandler" /> that rethrows all exceptions
    /// </summary>
    public class RethrowSceneExceptionsHandler : ISceneExceptionsHandler
    {
        public void Dispose() { }

        public ISystemGroupExceptionHandler.Action Handle(Exception exception, Type systemGroupType) =>
            throw exception;

        public void OnJavaScriptException(Exception exception)
        {
            throw exception;
        }

        public void OnEngineException(Exception exception, string category = ReportCategory.ENGINE)
        {
            throw exception;
        }

        public UniTask<T> ReportAndRethrowException<T>(UniTask<T> task) =>
            task;

        public UniTask ReportAndRethrowException(UniTask task) =>
            task;

        public void ReportApiException(Exception e)
        {
        }
    }
}
