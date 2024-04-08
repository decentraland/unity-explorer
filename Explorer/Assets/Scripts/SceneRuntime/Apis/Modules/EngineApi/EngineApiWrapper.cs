using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : IJsApiWrapper
    {
        internal readonly IEngineApi api;

        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly ISceneExceptionsHandler exceptionsHandler;

        private byte[] lastInput;

        public EngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;
        }

        public void Dispose()
        {
            // Dispose the last input buffer
            if (lastInput != null)
                instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);

            lastInput = null;

            // Dispose the engine API Implementation
            // It will dispose its buffers
            api.Dispose();
        }

        [UsedImplicitly]
        public ScriptableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", "CrdtSendToRenderer");

                var intLength = (int)data.Length;

                if (lastInput == null || lastInput.Length < intLength)
                {
                    // Release the old one
                    if (lastInput != null)
                        instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);

                    // Rent a new one
                    lastInput = instancePoolsProvider.GetCrdtRawDataPool(intLength);
                }

                // V8ScriptItem does not support zero length
                if (data.Length > 0)

                    // otherwise use the existing one
                    data.Read(0, data.Length, lastInput, 0);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.AsMemory().Slice(0, intLength));

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

        [UsedImplicitly]
        public ScriptableByteArray CrdtGetState()
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

        public void SetIsDisposing()
        {
            api.SetIsDisposing();
        }
    }
}
