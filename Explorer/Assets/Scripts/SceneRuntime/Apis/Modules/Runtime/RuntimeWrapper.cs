using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.Runtime
{
    public class RuntimeWrapper : IJsApiWrapper
    {
        internal readonly IRuntime api;

        private readonly ISceneExceptionsHandler exceptionsHandler;

        private readonly CancellationTokenSource cancellationTokenSource;

        public RuntimeWrapper(IRuntime api, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.exceptionsHandler = exceptionsHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            // Dispose the engine API Implementation
            // It will dispose its buffers
            api.Dispose();

            cancellationTokenSource.SafeCancelAndDispose();
        }

        [UsedImplicitly]
        public object ReadFile(string fileName)
        {
            try { return api.ReadFileAsync(fileName, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e)
            {
                // Report an uncategorized exception
                exceptionsHandler.OnEngineException(e);
                return null;
            }
        }

        [UsedImplicitly]
        public object GetRealm() =>
            api.GetRealmAsync(cancellationTokenSource.Token).ToPromise();

        [UsedImplicitly]
        public object GetWorldTime() =>
            api.GetWorldTimeAsync(cancellationTokenSource.Token).ToPromise();

        [UsedImplicitly]
        public object GetSceneInformation() =>
            api.GetSceneInformation();
    }
}
