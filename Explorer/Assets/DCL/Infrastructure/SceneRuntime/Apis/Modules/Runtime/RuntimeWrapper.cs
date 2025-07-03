using JetBrains.Annotations;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.Runtime
{
    public class RuntimeWrapper : JsApiWrapper<IRuntime>
    {
        private readonly ISceneExceptionsHandler exceptionsHandler;

        public RuntimeWrapper(IRuntime api, ISceneExceptionsHandler exceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.exceptionsHandler = exceptionsHandler;
        }

        [UsedImplicitly]
        public object ReadFile(string fileName)
        {
            try { return api.ReadFileAsync(fileName, disposeCts.Token).ToDisconnectedPromise(this); }
            catch (Exception e)
            {
                // Report an uncategorized exception
                exceptionsHandler.OnEngineException(e);
                return null!;
            }
        }

        [UsedImplicitly]
        public object GetRealm() =>
            api.GetRealmAsync(disposeCts.Token).ToDisconnectedPromise(this);

        [UsedImplicitly]
        public object GetWorldTime() =>
            api.GetWorldTimeAsync(disposeCts.Token).ToDisconnectedPromise(this);

        [UsedImplicitly]
        public object GetSceneInformation() =>
            api.GetSceneInformation();
    }
}
