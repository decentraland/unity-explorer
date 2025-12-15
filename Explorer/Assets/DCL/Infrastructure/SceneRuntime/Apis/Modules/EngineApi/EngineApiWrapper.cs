using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
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
        ): base(api, disposeCts)
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

                // Avoid copying of the buffer
                // InvokeWithDirectAccess<TArg, TResult>(Func<IntPtr, TArg, TResult>, TArg)
                PoolableByteArray result = data.InvokeWithDirectAccess(
                    static (ptr, args) => {
                        args.singleMemoryManager.Assign(ptr, (int) args.length);
                        return args.api.CrdtSendToRenderer(args.singleMemoryManager.Memory);
                    },
                    (api, length, singleMemoryManager)
                );

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
