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
        private readonly IJsOperations jsOperations;
        protected readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly SceneRuntimeMetrics metrics;
        private readonly string threadName;
        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        public EngineApiWrapper(IEngineApi api, ISceneData sceneData, IInstancePoolsProvider instancePoolsProvider, IJsOperations jsOperations, ISceneExceptionsHandler exceptionsHandler, SceneRuntimeMetrics metrics, CancellationTokenSource disposeCts)
            : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.jsOperations = jsOperations;
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
        public ITypedArray<byte>? CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return null;

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", threadName);

                instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(data, ref lastInput);

                metrics.BytesFromScene.Add(lastInput.Length);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.Memory);

                metrics.BytesToScene.Add(result.Length);

                Profiler.EndThreadProfiling();

                return ToScriptArray(ref result);
            }
            catch (Exception e)
            {
                if (!disposeCts.IsCancellationRequested)

                    // Report an uncategorized MANAGED exception (don't propagate it further)
                    exceptionsHandler.OnEngineException(e);

                return null;
            }
        }

        [UsedImplicitly]
        public ITypedArray<byte>? CrdtGetState()
        {
            if (disposeCts.IsCancellationRequested)
                return null;

            try
            {
                PoolableByteArray result = api.CrdtGetState();
                metrics.BytesToScene.Add(result.Length);
                return ToScriptArray(ref result);
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return null;
            }
        }

        /// <summary>
        ///     Copies the payload into a script-owned Uint8Array in a single bulk write and returns the pooled
        ///     array deterministically. Previously the <see cref="PoolableByteArray" /> itself was returned to JS:
        ///     it was consumed through the per-byte fast-proxy enumerator (one host transition per byte in
        ///     <c>new Uint8Array(...)</c>) and never disposed, so the shared bytes pool never got its arrays back.
        /// </summary>
        private ITypedArray<byte>? ToScriptArray(ref PoolableByteArray result)
        {
            if (result.IsEmpty)
            {
                result.Dispose();
                return null;
            }

            ITypedArray<byte> scriptArray = jsOperations.NewUint8Array(result.Length);
            scriptArray.WriteBytes(result.Array, 0ul, (ulong)result.Length, 0ul);
            result.Dispose();
            return scriptArray;
        }

        [UsedImplicitly]
        public virtual PoolableSDKObservableEventArray? SendBatch() => null;
    }
}
