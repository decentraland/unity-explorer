using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8.FastProxy;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using System.Threading;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>, IV8FastHostObject
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        private static readonly V8FastHostObjectOperations<EngineApiWrapper> OPERATIONS = new();
        IV8FastHostObjectOperations IV8FastHostObject.Operations => OPERATIONS;

        static EngineApiWrapper()
        {
            OPERATIONS.Configure(static configuration => Configure(configuration));
        }

        public EngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler, CancellationTokenSource disposeCts)
            : base(api, disposeCts)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;
        }

        protected override void DisposeInternal()
        {
            // Dispose the last input buffer
            lastInput.ReleaseAndDispose();
        }

        private ScriptableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return ScriptableByteArray.EMPTY;

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", "CrdtSendToRenderer");

                instancePoolsProvider.RenewCrdtRawDataPoolFromScriptArray(data, ref lastInput);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.Memory);

                Profiler.EndThreadProfiling();

                return result.IsEmpty ? ScriptableByteArray.EMPTY : new ScriptableByteArray(result);
            }
            catch (Exception e)
            {
                if (!disposeCts.IsCancellationRequested)

                    // Report an uncategorized MANAGED exception (don't propagate it further)
                    exceptionsHandler.OnEngineException(e);

                return ScriptableByteArray.EMPTY;
            }
        }

        private ScriptableByteArray CrdtGetState()
        {
            if (disposeCts.IsCancellationRequested)
                return ScriptableByteArray.EMPTY;

            try
            {
                PoolableByteArray result = api.CrdtGetState();
                return result.IsEmpty ? ScriptableByteArray.EMPTY : new ScriptableByteArray(result);
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return ScriptableByteArray.EMPTY;
            }
        }

        protected virtual ScriptableSDKObservableEventArray? SendBatch() => null;

        protected static void Configure<T>(V8FastHostObjectConfiguration<T> configuration)
            where T: EngineApiWrapper
        {
            configuration.AddMethodGetter(nameof(CrdtGetState),
                static (T self, in V8FastArgs _, in V8FastResult result) =>
                    result.Set(self.CrdtGetState()));

            configuration.AddMethodGetter(nameof(CrdtSendToRenderer),
                static (T self, in V8FastArgs args, in V8FastResult _) =>
                    self.CrdtSendToRenderer(args.Get<ITypedArray<byte>>(0)));

            configuration.AddMethodGetter(nameof(SendBatch),
                static (T self, in V8FastArgs _, in V8FastResult result) =>
                    result.Set(self.SendBatch()));
        }
    }
}
