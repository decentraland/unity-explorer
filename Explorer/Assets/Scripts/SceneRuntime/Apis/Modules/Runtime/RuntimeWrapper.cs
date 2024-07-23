using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.Runtime
{
    public class RuntimeWrapper : JsApiWrapperBase<IRuntime>
    {
        private readonly ISceneExceptionsHandler exceptionsHandler;

        private readonly CancellationTokenSource cancellationTokenSource;

        public RuntimeWrapper(IRuntime api, ISceneExceptionsHandler exceptionsHandler) : base(api)
        {
            this.exceptionsHandler = exceptionsHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected override void DisposeInternal()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        [UsedImplicitly]
        public object ReadFile(string fileName)
        {
            try { return api.ReadFileAsync(fileName, cancellationTokenSource.Token).ToDisconnectedPromise(); }
            catch (Exception e)
            {
                // Report an uncategorized exception
                exceptionsHandler.OnEngineException(e);
                return null;
            }
        }

        [UsedImplicitly]
        public object GetRealm() =>
            api.GetRealmAsync(cancellationTokenSource.Token).ToDisconnectedPromise();

        [UsedImplicitly]
        public object GetWorldTime() =>
            api.GetWorldTimeAsync(cancellationTokenSource.Token).ToDisconnectedPromise();

        [UsedImplicitly]
        public object GetSceneInformation() =>
            api.GetSceneInformation();
    }
}
