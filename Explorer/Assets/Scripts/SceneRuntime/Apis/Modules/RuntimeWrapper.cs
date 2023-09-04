using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules
{
    public class RuntimeWrapper : IDisposable
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

        [UsedImplicitly]
        public object ReadFile(string fileName)
        {
            try
            {
                Task<ITypedArray<byte>> res = api.ReadFile(fileName, cancellationTokenSource.Token).AsTask();
                object promise = res.ToPromise();
                return promise;
            }
            catch (Exception e)
            {
                // Report an uncategorized exception
                exceptionsHandler.OnEngineException(e);
                return null;
            }
        }

        public void Dispose()
        {
            // Dispose the engine API Implementation
            // It will dispose its buffers
            api.Dispose();

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
