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
using Utility;
using Utility.Multithreading;

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

        // ResetableSource is an optimization to reduce 11kb of memory allocation per Update (reduces 15kb to 4kb per update)
        private readonly JSTaskResolverResetable resetableSource;

        private readonly CancellationTokenSource isDisposingTokenSource = new ();
        private readonly InterlockedFlag isDisposing = new ();
        private int nextUint8Array;

        private ScriptObject updateFunc;
        private ScriptObject startFunc;
        private EngineApiWrapper? engineApi;

        // Golden-capture determinism: when DCL_GOLDEN_SCENE_CLOCK=1, the scene runs on a fully
        // quantized clock — every tick feeds exactly FIXED_TICK_DELTA and Date.now/performance.now
        // report the accumulated tick time, until the ceiling (exactly 7200 ticks) is reached; from
        // then on dt stays 0 and the clock holds constant. Scene state is therefore a pure function
        // of tick count: dt-accumulators, Date-anchored logic, and (with the seeded Math.random)
        // per-tick random consumers all reproduce identically across runs regardless of boot
        // history. A ceiling rather than a catch-up jump: scenes clamp oversized deltas.
        // The clock is phase-locked: BOOTSTRAP_HOLD_MS of ticks let the scene run its setup and
        // request its content, then dispatch holds (invisibly to the scene) until the real-time
        // loading window has elapsed, then ticking resumes to the ceiling. Loading states
        // quantized to bucket boundaries (see WriteGltfContainerLoadingStateSystem) all arrive on
        // the first resumed tick regardless of machine speed, so the post-resume evolution —
        // including every on-ready reaction — is a pure function of tick count. Bootstrap is just
        // 60 ticks — enough for setup issued from main() and frame-staged spawns — because scene
        // loops crawl (~2.5 Hz measured) while the world loads and the phase hold only binds, and
        // with it the canonical tick-to-wall anchoring, if every scene reaches its bootstrap end
        // before the release; the post-resume runway must still settle before the visual freeze
        // at the reduced tick rate of throttled neighbor scenes.
        private const double FROZEN_SCENE_CLOCK_MS = 240_000.0;
        private const double BOOTSTRAP_HOLD_MS = 2_000.0;
        private const float FIXED_TICK_DELTA = 1f / 30f;
        private static readonly double HOLD_UNTIL_SECONDS = double.TryParse(
            Environment.GetEnvironmentVariable("DCL_GOLDEN_READY_WAIT"), out double w) ? w : 240.0;
        private readonly bool determClock = Environment.GetEnvironmentVariable("DCL_GOLDEN_SCENE_CLOCK") == "1";
        private ScriptObject? setSimTime;

        // Number of quantized-clock runtimes on their post-hold runway that have not yet reached
        // the ceiling, published through an AppDomain slot so the capture harness can report it
        // without an assembly reference. A runtime counts from its first resumed tick until the
        // tick that reaches the ceiling (or its disposal, if it never does); a runtime that never
        // resumes (0 FPS bucket) holds a fixed tick count and is not counted.
        private static readonly object BELOW_CEILING_LOCK = new ();
        private static int clocksBelowCeiling;
        private bool countedBelowCeiling;

        private static bool HoldElapsed() =>
            (DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds >= HOLD_UNTIL_SECONDS;

        // The capture harness raises the scene hold one lockstep frame ahead of the visual freeze
        // and lowers it again when that frame does not freeze, so dispatch resumes until the next
        // approach; once the freeze lands the frozen slot stops dispatch for good.
        private static bool SceneHeld() =>
            (AppDomain.CurrentDomain.GetData("golden.sceneHold") as bool?) == true
            || (AppDomain.CurrentDomain.GetData("golden.frozen") as bool?) == true;

        private double sceneClockMs;

        public bool LastUpdateDispatched { get; private set; } = true;

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

            // Route the scene's JS clock through the client-fed quantized tick time from the very
            // first evaluation (module-init Date.now anchors then repeat identically across runs),
            // and replace Math.random with a fixed-seed xorshift32 so random draws repeat too.
            if (determClock)
                engine.Execute(@"(function(){var __t=1;globalThis.__dclSetSimTime=function(ms){__t=ms;};Date.now=function(){return __t;};if(!globalThis.performance)globalThis.performance={};globalThis.performance.now=function(){return __t;};var __s=2463534242;Math.random=function(){__s^=__s<<13;__s^=__s>>>17;__s^=__s<<5;__s>>>=0;return __s/4294967296;};})();");

            // Setup unitask resolver
            engine.AddHostObject("__resetableSource", resetableSource);

            arrayCtor = (ScriptObject)engine.Global.GetProperty("Array");
            unit8ArrayCtor = (ScriptObject)engine.Global.GetProperty("Uint8Array");
            uint8Arrays = new List<ITypedArray<byte>>();
            nextUint8Array = 0;
        }

        /// <remarks>
        ///     <c>SceneFacade</c> is a component in the global scene as an
        ///     <see cref="ISceneFacade" />. It owns its <see cref="SceneRuntimeImpl" /> through its
        ///     <c>deps</c> field, which in turns owns its <see cref="V8ScriptEngine" />. So that also
        ///     shall be the chain of Dispose calls.
        /// </remarks>
        public void Dispose()
        {
            LeaveBelowCeiling();
            engine.Dispose();
            jsApiBunch.Dispose();
        }

        private void EnterBelowCeiling()
        {
            if (countedBelowCeiling) return;

            countedBelowCeiling = true;
            PublishClocksBelowCeiling(+1);
        }

        private void LeaveBelowCeiling()
        {
            if (!countedBelowCeiling) return;

            countedBelowCeiling = false;
            PublishClocksBelowCeiling(-1);
        }

        // Runtimes tick and are disposed off the main thread; the count changes and publishes under
        // one lock so the slot never keeps a value that a concurrent change has superseded.
        private static void PublishClocksBelowCeiling(int delta)
        {
            lock (BELOW_CEILING_LOCK)
            {
                clocksBelowCeiling += delta;
                AppDomain.CurrentDomain.SetData("golden.sceneClocksBelowCeiling", clocksBelowCeiling);
            }
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

        /// <remarks>
        ///     Entered concurrently from more than one thread, so the single-entry guard is atomic.
        /// </remarks>
        public void SetIsDisposing()
        {
            if (!isDisposing.Set())
                return;

            isDisposingTokenSource.Cancel();
            isDisposingTokenSource.Dispose();
        }

        public void Interrupt()
        {
            // V8ScriptEngine.Interrupt is thread-safe; causes ScriptInterruptedException
            // to be thrown from any active JS execution on this engine.
            try { engine.Interrupt(); }
            catch (ObjectDisposedException) { /* engine already disposed — nothing to do */ }
        }

        public UniTask StartScene()
        {
            resetableSource.Reset();
            startFunc.InvokeAsFunction();
            return resetableSource.Task;
        }

        public UniTask UpdateScene(float dt)
        {
            nextUint8Array = 0;
            RuntimeHeapInfo = engine.GetRuntimeHeapInfo();

            if (determClock)
            {
                LastUpdateDispatched = false;

                // Phase hold: after the bootstrap ticks the scene stops receiving calls at all
                // until the world has had its real-time loading window. The hold is invisible to
                // the scene (no call, no clock movement), so the tick count and clock timeline
                // are identical on every machine no matter how many frames its loading took;
                // engine-side loads keep completing meanwhile and their quantized loading states
                // all flush on the first resumed tick's bucket boundary.
                if (sceneClockMs >= BOOTSTRAP_HOLD_MS && sceneClockMs < FROZEN_SCENE_CLOCK_MS && !HoldElapsed())
                    return UniTask.CompletedTask;

                // Once the scene hold is raised, every scene holds its state whether or not its
                // clock reached the ceiling: a tick dispatched after the freeze re-issues CRDT
                // output (tween restarts, material writes) on top of the normalized frame.
                if (SceneHeld())
                    return UniTask.CompletedTask;

                dt = NextDeterministicDelta();

                // Past the ceiling the clock no longer advances, but the CALL COUNT still
                // would: scene state that mutates per invocation (frame counters, random
                // draws, dt-independent state machines) accumulates with the boot-dependent
                // number of post-ceiling frames. Stop dispatching entirely so every run's
                // scenes hold the exact final-tick state. dt == 0 only on post-ceiling calls,
                // so the tick that reaches the ceiling still runs.
                if (dt == 0f && sceneClockMs >= FROZEN_SCENE_CLOCK_MS)
                    return UniTask.CompletedTask;

                LastUpdateDispatched = true;
            }

            resetableSource.Reset();
            updateFunc.InvokeAsFunction(dt);
            return resetableSource.Task;
        }

        private float NextDeterministicDelta()
        {
            setSimTime ??= engine.Global.GetProperty("__dclSetSimTime") as ScriptObject;

            if (sceneClockMs >= FROZEN_SCENE_CLOCK_MS)
            {
                setSimTime?.InvokeAsFunction(FROZEN_SCENE_CLOCK_MS);
                return 0f;
            }

            // Reached only outside the phase hold, so a clock past its bootstrap is on its runway.
            if (sceneClockMs >= BOOTSTRAP_HOLD_MS)
                EnterBelowCeiling();

            sceneClockMs += FIXED_TICK_DELTA * 1000.0;
            setSimTime?.InvokeAsFunction(sceneClockMs);

            if (sceneClockMs >= FROZEN_SCENE_CLOCK_MS)
                LeaveBelowCeiling();

            return FIXED_TICK_DELTA;
        }

        public void ApplyStaticMessages(ReadOnlyMemory<byte> data)
        {
            PoolableByteArray result = engineApi.EnsureNotNull().api.CrdtSendToRenderer(data, false);

            // Initial messages are not expected to return anything
            Assert.IsTrue(result.IsEmpty);
        }

        ScriptObject IJsOperations.NewArray() =>
            (ScriptObject)arrayCtor.Invoke(true);

        ITypedArray<byte> IJsOperations.NewUint8Array(int length) =>
            (ITypedArray<byte>)unit8ArrayCtor.Invoke(true, length);

        ITypedArray<byte> IJsOperations.GetTempUint8Array()
        {
            if (nextUint8Array >= uint8Arrays.Count)
                uint8Arrays.Add((ITypedArray<byte>)unit8ArrayCtor.Invoke(true,
                    IJsOperations.LIVEKIT_MAX_SIZE));

            return uint8Arrays[nextUint8Array++];
        }
    }
}
