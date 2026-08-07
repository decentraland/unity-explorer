using CrdtEcsBridge.PoolsProviders;
using DCL.Profiling;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Utility;
using Profiler = UnityEngine.Profiling.Profiler;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly IJsOperations jsOperations;
        private readonly SceneRuntimeMetrics metrics;
        private readonly string threadName;
        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        private ITypedArray<byte>? emptyResult;

        public EngineApiWrapper(IEngineApi api, ISceneData sceneData, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler, SceneRuntimeMetrics metrics, IJsOperations jsOperations, CancellationTokenSource disposeCts)
            : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;
            this.jsOperations = jsOperations;
            this.metrics = metrics;
            threadName = $"CrdtSendToRenderer({sceneData.SceneShortInfo})";
        }

        protected override void DisposeInternal()
        {
            // Dispose the last input buffer
            lastInput.ReleaseAndDispose();
        }

        [UsedImplicitly]
        public object? CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return EmptyResultOrNull();

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", threadName);

                instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(data, ref lastInput);

                metrics.BytesFromScene.Add(lastInput.Length);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.Memory);

                metrics.BytesToScene.Add(result.Length);

                Profiler.EndThreadProfiling();

                return ToScriptUint8Array(result);
            }
            catch (Exception e)
            {
                if (!disposeCts.IsCancellationRequested)

                    // Report an uncategorized MANAGED exception (don't propagate it further)
                    exceptionsHandler.OnEngineException(e);

                return EmptyResultOrNull();
            }
        }

        [UsedImplicitly]
        public object? CrdtGetState()
        {
            if (disposeCts.IsCancellationRequested)
                return EmptyResultOrNull();

            try
            {
                PoolableByteArray result = api.CrdtGetState();
                metrics.BytesToScene.Add(result.Length);
                return ToScriptUint8Array(result);
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return EmptyResultOrNull();
            }
        }

        private object ToScriptUint8Array(PoolableByteArray result)
        {
            if (result.IsEmpty)
            {
                result.Dispose();
                return emptyResult ??= jsOperations.NewUint8Array(0);
            }

            ITypedArray<byte> js = jsOperations.NewUint8Array(result.Length);

            js.Write(result.Memory, (ulong)result.Length, 0);

            result.Dispose();
            return js;
        }

        private object? EmptyResultOrNull()
        {
            if (emptyResult != null)
                return emptyResult;

            try { return emptyResult = jsOperations.NewUint8Array(0); }
            catch (ObjectDisposedException) { return null; }
        }

        [UsedImplicitly]
        public virtual PoolableSDKObservableEventArray? SendBatch() => null;
    }
}
