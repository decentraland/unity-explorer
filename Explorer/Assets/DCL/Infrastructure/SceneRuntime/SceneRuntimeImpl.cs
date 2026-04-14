using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SceneRunner.Scene;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.ModuleHub;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Assertions;

namespace SceneRuntime
{
    // Avoid the same name for Namespace and Class
    public sealed class SceneRuntimeImpl : ISceneRuntime, IJsOperations
    {
        internal readonly V8ScriptEngine engine;
        private readonly ScriptObject arrayCtor;
        private readonly ScriptObject unit8ArrayCtor;
        private readonly List<ITypedArray<byte>> uint8Arrays;
        private readonly JsApiBunch jsApiBunch;
        private readonly List<Action> preUpdateActions = new ();

        // Tracks the thread currently executing JS inside this scene's V8 engine.
        // Used to detect same-thread re-entrancy: host callbacks must not call back into V8
        // (e.g. construct new JS objects) while V8 is already executing on the same thread,
        // because the inner invocation can trigger GC which frees objects the outer frame holds.
        private Thread? v8ExecutingThread;

        // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
        private readonly JSTaskResolverResetable resetableSource;

        private readonly CancellationTokenSource isDisposingTokenSource = new ();
        private int nextUint8Array;

        private ScriptObject updateFunc;
        private ScriptObject startFunc;
        private EngineApiWrapper? engineApi;

        public V8RuntimeHeapInfo RuntimeHeapInfo { get; private set; }

        CancellationTokenSource ISceneRuntime.isDisposingTokenSource => isDisposingTokenSource;

        public SceneRuntimeImpl(
            string sourceCode,
            string initCode,
            IReadOnlyDictionary<string, string> jsModules,
            SceneShortInfo sceneShortInfo,
            V8EngineFactory engineFactory
        )
        {
            resetableSource = new JSTaskResolverResetable();

            engine = engineFactory.Create(sceneShortInfo);
            jsApiBunch = new JsApiBunch(engine);

            var moduleHub = new SceneModuleHub(engine);

            moduleHub.LoadAndCompileJsModules(jsModules);

            // Compile Scene Code
            V8Script sceneScript = engine.Compile(sourceCode).EnsureNotNull();

            // Initialize init API
            // TODO: This is only needed for the LifeCycle
            var unityOpsApi = new UnityOpsApi(engine, moduleHub, sceneScript, sceneShortInfo);
            engine.AddHostObject("UnityOpsApi", unityOpsApi);

            // engine.Execute(initCode.validateCode!);
            engine.Execute(initCode);

            // Set global SDK configuration flags
            engine.Execute("globalThis.ENABLE_SDK_TWEEN_SEQUENCE = false;");

            // Setup unitask resolver
            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (ScriptObject)engine.Global.GetProperty("Array");
            unit8ArrayCtor = (ScriptObject)engine.Global.GetProperty("Uint8Array");
            uint8Arrays = new List<ITypedArray<byte>>();
            nextUint8Array = 0;
        }

        /// <remarks>
        ///     <see cref="SceneFacade" /> is a component in the global scene as an
        ///     <see cref="ISceneFacade" />. It owns its <see cref="SceneRuntimeImpl" /> through its
        ///     <see cref="deps" /> field, which in turns owns its <see cref="V8ScriptEngine" />. So that also
        ///     shall be the chain of Dispose calls.
        /// </remarks>
        public void Dispose()
        {
            jsApiBunch.Dispose();
            engine.Dispose();
        }

        public void ExecuteSceneJson()
        {
            engine.Execute(@"
            const __internalScene = require('~scene.js')
            const __internalOnStart = async function () {
                try {
                    await __internalScene.onStart()
                    __resetableSource.Completed()
                } catch (e) {
                    __resetableSource.Reject(e.stack)
                }
            }
            const __internalOnUpdate = async function (dt) {
                try {
                    await __internalScene.onUpdate(dt)
                    __resetableSource.Completed()
                } catch(e) {
                    __resetableSource.Reject(e.stack)
                }
            }
        ");

            updateFunc = (ScriptObject)engine.Evaluate("__internalOnUpdate").EnsureNotNull();
            startFunc = (ScriptObject)engine.Evaluate("__internalOnStart").EnsureNotNull();

            // Pre-allocate temp Uint8Array pool to avoid re-entrant V8 calls from host callbacks.
            // Calling unit8ArrayCtor.Invoke() while V8 is already executing JS (e.g. from GetTempUint8Array
            // inside a host callback) causes use-after-free crashes because the inner invocation can trigger GC.
            const int PREWARM_COUNT = 16;
            for (int i = 0; i < PREWARM_COUNT; i++)
                uint8Arrays.Add((ITypedArray<byte>)unit8ArrayCtor.Invoke(true, IJsOperations.LIVEKIT_MAX_SIZE));
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            jsApiBunch.OnSceneIsCurrentChanged(isCurrent);
        }

        public void RegisterEngineAPIWrapper(EngineApiWrapper newWrapper)
        {
            engineApi = newWrapper;
        }

        public void Register<T>(string itemName, T target) where T: JsApiWrapper
        {
            jsApiBunch.AddHostObject(itemName, target);
        }

        public void SetIsDisposing()
        {
            isDisposingTokenSource.Cancel();
            isDisposingTokenSource.Dispose();
        }

        public UniTask StartScene()
        {
            resetableSource.Reset();
            v8ExecutingThread = Thread.CurrentThread;
            try { startFunc.InvokeAsFunction(); }
            finally { v8ExecutingThread = null; }
            return resetableSource.Task;
        }

        public UniTask UpdateScene(float dt)
        {
            nextUint8Array = 0;
            RuntimeHeapInfo = engine.GetRuntimeHeapInfo();

            // Run pre-update actions before V8 starts executing.
            // These are V8 operations (property writes, method calls, object construction) that
            // host callbacks register to avoid performing them re-entrantly during JS execution.
            foreach (Action action in preUpdateActions)
                action();

            resetableSource.Reset();
            v8ExecutingThread = Thread.CurrentThread;
            try { updateFunc.InvokeAsFunction(dt); }
            finally { v8ExecutingThread = null; }
            return resetableSource.Task;
        }

        public void ApplyStaticMessages(ReadOnlyMemory<byte> data)
        {
            PoolableByteArray result = engineApi.EnsureNotNull().api.CrdtSendToRenderer(data, false);

            // Initial messages are not expected to return anything
            Assert.IsTrue(result.IsEmpty);
        }

        ScriptObject IJsOperations.NewArray()
        {
            ThrowIfReentrant(nameof(IJsOperations.NewArray));
            return (ScriptObject)arrayCtor.Invoke(true);
        }

        ITypedArray<byte> IJsOperations.NewUint8Array(int length)
        {
            ThrowIfReentrant(nameof(IJsOperations.NewUint8Array));
            return (ITypedArray<byte>)unit8ArrayCtor.Invoke(true, length);
        }

        ITypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
            {
                ThrowIfReentrant(nameof(IJsOperations.GetTempUint8Array));
                uint8Arrays.Add((ITypedArray<byte>)unit8ArrayCtor.Invoke(true,
                    IJsOperations.LIVEKIT_MAX_SIZE));
            }

            return uint8Arrays[nextUint8Array++];
        }

        void IJsOperations.AddPreUpdateAction(Action action)
        {
            preUpdateActions.Add(action);
        }

        /// <summary>
        /// Throws if the calling thread is the same thread currently executing JS inside this engine.
        /// Any V8 object construction (Invoke on a constructor) from a host callback re-enters V8 on the
        /// same thread while its lock is already held. The inner invocation can trigger GC, freeing heap
        /// objects the outer execution frame still references, causing use-after-free crashes.
        /// </summary>
        private void ThrowIfReentrant(string callerName)
        {
            if (v8ExecutingThread == Thread.CurrentThread)
                throw new InvalidOperationException(
                    $"Re-entrant V8 call detected in {callerName}: a host callback is attempting to construct " +
                    "a new V8 object while V8 is already executing JS on this thread. " +
                    "This can trigger GC inside an active V8 frame and cause use-after-free crashes. " +
                    "Pre-allocate any required JS objects before calling InvokeAsFunction.");
        }
    }
}
