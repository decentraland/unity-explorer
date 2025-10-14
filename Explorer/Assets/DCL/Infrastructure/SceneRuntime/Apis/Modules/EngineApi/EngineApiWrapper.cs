using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.FastProxy;
using Microsoft.ClearScript.V8.SplitProxy;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using System.Threading;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>, IV8FastHostObject, IV8FastHostObjectOperations
    {
        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        IV8FastHostObjectOperations IV8FastHostObject.Operations => this;

        private static readonly V8FastHostMethodInvoker<EngineApiWrapper> CRDT_SEND_TO_RENDERER =
            static (EngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                self.CrdtSendToRenderer(args.GetUint8Array(0));

        private static readonly V8FastHostMethodInvoker<EngineApiWrapper> CRDT_GET_STATE =
            static (EngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                result.Set(self.CrdtGetState());

        private readonly V8FastHostMethodInvoker<EngineApiWrapper> sendBatch =
            static (EngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                result.Set(self.SendBatch());

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

        private ScriptableByteArray CrdtSendToRenderer(Uint8Array data)
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

        private void SendBatch(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            result.SetHostObject(SendBatch());
        }

        protected virtual ScriptableSDKObservableEventArray? SendBatch() => null;

        void IV8FastHostObjectOperations.GetProperty(IV8FastHostObject instance, string name, in V8FastResult value, out bool isCacheable) =>
            GetProperty(name, value, out isCacheable);

        protected virtual void GetProperty(string name, V8FastResult value, out bool isCacheable)
        {
            isCacheable = true;

            if (name.Equals(nameof(CrdtSendToRenderer)))
                value.Set(CRDT_SEND_TO_RENDERER);
            else if (name.Equals(nameof(CrdtGetState)))
                value.SetHostObject(CRDT_GET_STATE);
            else if (name.Equals(nameof(SendBatch)))
                value.SetHostObject(sendBatch);
            else
                throw new NotImplementedException(
                    $"Named property {name.ToString()} is not implemented");
        }
    }
}
