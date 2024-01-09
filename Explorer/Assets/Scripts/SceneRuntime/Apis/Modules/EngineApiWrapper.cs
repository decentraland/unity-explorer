using CrdtEcsBridge.Engine;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using UnityEngine.Profiling;
using Utility;

namespace SceneRuntime.Apis.Modules
{
    public class EngineApiWrapper : IDisposable
    {
        internal readonly IEngineApi api;

        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly ISceneExceptionsHandler exceptionsHandler;

        private CancellationTokenSource sendCancellationToken;
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

                ArraySegment<byte> result = api.CrdtSendToRenderer(lastInput.AsMemory().Slice(0, intLength));

                Profiler.EndThreadProfiling();

                return result.Count > 0 ? new ScriptableByteArray(result) : ScriptableByteArray.EMPTY;
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
                ArraySegment<byte> result = api.CrdtGetState();
                return result.Count > 0 ? new ScriptableByteArray(result) : ScriptableByteArray.EMPTY;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return ScriptableByteArray.EMPTY;
            }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object SendEthereumMessageAsync(int id, string method, string jsonParams)
        {
            // TODO: support cancellations by id (?)
            sendCancellationToken = sendCancellationToken.SafeRestart();

            return api.SendEthereumMessageAsync(method, JsonConvert.DeserializeObject<object[]>(jsonParams), sendCancellationToken.Token)
                      .AsTask()
                      .ToPromise();
        }

        public void SetIsDisposing()
        {
            api.SetIsDisposing();
        }
    }
}
