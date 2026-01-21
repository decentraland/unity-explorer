using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Profiling;
using Utility.Memory;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>
    {
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private readonly string threadName;
        private readonly SingleUnmanagedMemoryManager<byte> singleMemoryManager = new ();

        public EngineApiWrapper(
            IEngineApi api,
            ISceneData sceneData,
            ISceneExceptionsHandler exceptionsHandler,
            CancellationTokenSource disposeCts
        ) : base(api, disposeCts)
        {
            this.exceptionsHandler = exceptionsHandler;
            threadName = $"CrdtSendToRenderer({sceneData.SceneShortInfo})";
        }

        [UsedImplicitly]
        public PoolableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return PoolableByteArray.EMPTY;

            // V8ScriptItem does not support zero length
            ulong length = data.Length;

            if (length == 0)
                return PoolableByteArray.EMPTY;

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", threadName);
                PoolableByteArray result = SendToRenderer(data, length);
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

        private PoolableByteArray SendToRenderer(ITypedArray<byte> data, ulong length)
        {
            // Avoid copying of the buffer
            // InvokeWithDirectAccess<TArg, TResult>(Func<IntPtr, TArg, TResult>, TArg)
            return data.InvokeWithDirectAccess(
                static (ptr, args) =>
                {
                    args.singleMemoryManager.Assign(ptr, (int)args.length);
                    return args.api.CrdtSendToRenderer(args.singleMemoryManager.Memory);
                },
                (api, length, singleMemoryManager)
            );
        }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public PoolableByteArray SendToRendererTest(ITypedArray<byte> data)
        {
            return SendToRenderer(data, data.Length);
        }

        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        public PoolableByteArray SendToRendererTestLegacy(ITypedArray<byte> data, IInstancePoolsProvider instancePoolsProvider)
        {
            RenewCrdtRawDataPoolFromScriptArray(instancePoolsProvider, data, ref lastInput);
            return api.CrdtSendToRenderer(lastInput.Memory);
        }

        private static void RenewCrdtRawDataPoolFromScriptArray(
            IInstancePoolsProvider instancePoolsProvider, ITypedArray<byte> scriptArray,
            ref PoolableByteArray lastInput)
        {
            EnsureArrayLength(instancePoolsProvider, (int)scriptArray.Length, ref lastInput);

            // V8ScriptItem does not support zero length
            if (scriptArray.Length > 0)
                scriptArray.Read(0, scriptArray.Length, lastInput.Array, 0);
        }

        private static void EnsureArrayLength(IInstancePoolsProvider instancePoolsProvider,
            int scriptArrayLength, ref PoolableByteArray lastInput)
        {
            // if the rented array can't keep the desired data, replace it
            if (lastInput.Array.Length < scriptArrayLength)
            {
                // Release the old one
                lastInput.Dispose();

                // Rent a new one
                lastInput = instancePoolsProvider.GetAPIRawDataPool(scriptArrayLength);
            }
            // Otherwise set the desired length to the existing array so it provides a correct span
            else
                lastInput.SetLength(scriptArrayLength);
        }
#endif

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
        public virtual PoolableSDKObservableEventArray? SendBatch() =>
            null;
    }
}
