using CrdtEcsBridge.PoolsProviders;
using DCL.Profiling;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Profiler = UnityEngine.Profiling.Profiler;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly SceneRuntimeMetrics metrics;
        private readonly string threadName;
        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        public EngineApiWrapper(IEngineApi api, ISceneData sceneData, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler, SceneRuntimeMetrics metrics, CancellationTokenSource disposeCts)
            : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;
            this.metrics = metrics;
            threadName = $"CrdtSendToRenderer({sceneData.SceneShortInfo})";
        }

        protected override void DisposeInternal()
        {
            // Dispose the last input buffer
            lastInput.ReleaseAndDispose();
        }

        [UsedImplicitly]
        public PoolableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return PoolableByteArray.EMPTY;

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", threadName);

                instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(data, ref lastInput);

                metrics.BytesFromScene.Add(lastInput.Length);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.Memory);

                metrics.BytesToScene.Add(result.Length);

                Profiler.EndThreadProfiling();

                return result.IsEmpty ? PoolableByteArray.EMPTY : result;
            }
            catch (Exception e)
            {
                if (!disposeCts.IsCancellationRequested)

                    // Report an uncategorized MANAGED exception (don't propagate it further)
                    exceptionsHandler.OnEngineException(e);

                return PoolableByteArray.EMPTY;
            }
        }

        [UsedImplicitly]
        public PoolableByteArray CrdtGetState()
        {
            if (disposeCts.IsCancellationRequested)
                return PoolableByteArray.EMPTY;

            try
            {
                PoolableByteArray result = api.CrdtGetState();
                metrics.BytesToScene.Add(result.Length);
                return result.IsEmpty ? PoolableByteArray.EMPTY : result;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return PoolableByteArray.EMPTY;
            }
        }

        [UsedImplicitly]
        public virtual PoolableSDKObservableEventArray? SendBatch() => null;
    }
}
