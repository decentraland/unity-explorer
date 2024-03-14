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

        public SceneApiWrapper(ISceneApi api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public object GetSceneInfo() =>
            api.GetSceneInfo();
    }
}
