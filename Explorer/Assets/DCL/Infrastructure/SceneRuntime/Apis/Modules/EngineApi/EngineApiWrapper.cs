using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.SplitProxy;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : IJsApiWrapper, IV8HostObject
    {
        internal readonly IEngineApi api;

        private readonly IInstancePoolsProvider instancePoolsProvider;
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        private readonly InvokeHostObject crdtSendToRenderer;
        private readonly InvokeHostObject crdtGetState;
        private readonly InvokeHostObject sendBatch;

        public EngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;

            crdtSendToRenderer = CrdtSendToRenderer;
            crdtGetState = CrdtGetState;
            sendBatch = SendBatch;
        }

        public void Dispose()
        {
            // Dispose the last input buffer
            lastInput.ReleaseAndDispose();
        }

        private void CrdtSendToRenderer(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            Uint8Array data = args[0].GetUint8Array();
            result.SetHostObject(CrdtSendToRenderer(data));
        }

        private ScriptableByteArray CrdtSendToRenderer(Uint8Array data)
        {
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
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return ScriptableByteArray.EMPTY;
            }
        }

        private void CrdtGetState(ReadOnlySpan<V8Value.Decoded> args, V8Value result) =>
            result.SetHostObject(CrdtGetState());

        private ScriptableByteArray CrdtGetState()
        {
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

        public void SetIsDisposing()
        {
            api.SetIsDisposing();
        }

        void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst) =>
            GetNamedProperty(name, value, out isConst);

        protected virtual void GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(CrdtSendToRenderer)))
                value.SetHostObject(crdtSendToRenderer);
            else if (name.Equals(nameof(CrdtGetState)))
                value.SetHostObject(crdtGetState);
            else if (name.Equals(nameof(SendBatch)))
                value.SetHostObject(sendBatch);
            else
                throw new NotImplementedException(
                    $"Named property {name.ToString()} is not implemented");
        }
    }
}
