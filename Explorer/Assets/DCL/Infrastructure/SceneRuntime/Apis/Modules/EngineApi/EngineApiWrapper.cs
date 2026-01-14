using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using System.Threading;
using UnityEngine.Profiling;
using Utility;
#if !UNITY_WEBGL
using Microsoft.ClearScript;
#endif

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly string threadName;
        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        public EngineApiWrapper(IEngineApi api, ISceneData sceneData, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler, CancellationTokenSource disposeCts)
            : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;
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

                IDCLTypedArray<byte> dclTypedArray = TypedArrayConverter.Convert(data);

                instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(dclTypedArray, ref lastInput);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.Memory);

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
