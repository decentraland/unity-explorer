using CrdtEcsBridge.Engine;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules
{
    public class RuntimeWrapper : IDisposable
    {
        internal readonly IRuntime api;

        private readonly ISceneExceptionsHandler exceptionsHandler;

        public RuntimeWrapper(IRuntime api, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.exceptionsHandler = exceptionsHandler;
        }

        [UsedImplicitly]
        public object ReadFile(string fileName)
        {
            try
            {
                var res = api.ReadFile(fileName).AsTask();
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
        }
    }
}
