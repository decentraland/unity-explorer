using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules
{
    public class SceneApiWrapper : IDisposable
    {
        private readonly ISceneApi api;
        private readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly CancellationTokenSource cancellationTokenSource;

        public SceneApiWrapper(ISceneApi api, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.exceptionsHandler = exceptionsHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public object GetSceneInfo()
        {
            try { return api.GetSceneInfoAsync(cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
