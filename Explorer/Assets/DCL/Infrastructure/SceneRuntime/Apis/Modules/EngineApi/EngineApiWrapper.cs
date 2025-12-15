using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8.FastProxy;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine.Profiling;
using Utility.Memory;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>, IV8FastHostObject
    {
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private readonly string threadName;
        private readonly SingleUnmanagedMemoryManager<byte> singleMemoryManager = new ();

        private static readonly V8FastHostObjectOperations<EngineApiWrapper> OPERATIONS = new();
        protected virtual IV8FastHostObjectOperations operations => OPERATIONS;
        IV8FastHostObjectOperations IV8FastHostObject.Operations => operations;

        static EngineApiWrapper()
        {
            OPERATIONS.Configure(static configuration => Configure(configuration));
        }

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

        private ScriptableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return ScriptableByteArray.EMPTY;

            // V8ScriptItem does not support zero length
            ulong length = data.Length;
            if (length == 0)
                return ScriptableByteArray.EMPTY;

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
                static (T self, in V8FastArgs args, in V8FastResult result) =>
                    result.Set(self.CrdtSendToRenderer(args.Get<ITypedArray<byte>>(0))));

            configuration.AddMethodGetter(nameof(SendBatch),
                static (T self, in V8FastArgs _, in V8FastResult result) =>
                    result.Set(self.SendBatch()));
        }
    }
}
