using CrdtEcsBridge.Engine;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules
{
    public class EngineApiWrapper : IDisposable
    {
        private readonly IEngineApi api;
        private readonly IInstancePoolsProvider instancePoolsProvider;

        private byte[] lastInput;

        public EngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider)
        {
            this.api = api;
            this.instancePoolsProvider = instancePoolsProvider;
        }

        [UsedImplicitly]
        public ScriptableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
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

            ArraySegment<byte> result = api.CrdtSendToRenderer(lastInput.AsMemory().Slice(0, intLength));

            Profiler.EndThreadProfiling();

            return result.Count > 0 ? new ScriptableByteArray(result) : ScriptableByteArray.EMPTY;
        }

        [UsedImplicitly]
        public ScriptableByteArray CrdtGetState()
        {
            ArraySegment<byte> result = api.CrdtGetState();
            return result.Count > 0 ? new ScriptableByteArray(result) : ScriptableByteArray.EMPTY;
        }

        public void SetIsDisposing()
        {
            api.SetIsDisposing();
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
    }
}
