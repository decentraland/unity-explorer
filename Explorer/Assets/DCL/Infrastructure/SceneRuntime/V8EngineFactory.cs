using DCL.Diagnostics;
using Microsoft.ClearScript.V8;
using UnityEngine;

namespace SceneRuntime
{
    public sealed class V8EngineFactory
    {
        private readonly Vector2Int debugBaseParcel;
        private readonly string debugLocalPath;
        private readonly bool waitForDebugger;

        public V8EngineFactory(Vector2Int debugBaseParcel, string debugLocalPath, bool waitForDebugger)
        {
            this.debugBaseParcel = debugBaseParcel;
            this.debugLocalPath = debugLocalPath;
            this.waitForDebugger = waitForDebugger;
        }

        public V8ScriptEngine Create(SceneShortInfo sceneInfo)
        {
            V8ScriptEngineFlags flags = V8ScriptEngineFlags.None;

            if (!string.IsNullOrWhiteSpace(debugLocalPath) && sceneInfo.BaseParcel == debugBaseParcel)
            {
                flags |= V8ScriptEngineFlags.EnableDebugging;

                if (waitForDebugger)
                    flags |= V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart;
            }

            var engine = new V8ScriptEngine(sceneInfo.ToString(), flags, 9222);

            // IL2CPP does not support dynamic bindings!
            engine.DisableDynamicBinding = true;
            engine.UseReflectionBindFallback = true;
            engine.AllowReflection = true;

            return engine;
        }

        public Vector2Int DebugBaseParcel => debugBaseParcel;

        public string DebugLocalPath => debugLocalPath;
    }
}
