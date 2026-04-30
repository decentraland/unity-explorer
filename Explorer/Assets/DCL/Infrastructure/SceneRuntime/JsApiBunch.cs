using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime
{
    public class JsApiBunch : IDisposable
    {
        private static readonly TimeSpan FINALIZATION_GRACE_TIMEOUT = TimeSpan.FromSeconds(5);

        internal readonly JsApiCompletionGate gate;

        private readonly V8ScriptEngine engine;
        private readonly List<JsApiWrapper> wraps = new ();

        public JsApiBunch(V8ScriptEngine engine, CancellationTokenSource disposeCts)
        {
            this.engine = engine;
            gate = new JsApiCompletionGate(disposeCts, FINALIZATION_GRACE_TIMEOUT);
        }

        public void AddHostObject<T>(string itemName, T target) where T: JsApiWrapper
        {
            target.completionGate = gate;
            engine.AddHostObject(itemName, target);
            wraps.Add(target);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            foreach (JsApiWrapper wrap in wraps)
                wrap.OnSceneIsCurrentChanged(isCurrent);
        }

        public void Dispose()
        {
            foreach (JsApiWrapper wrap in wraps)
                wrap.Dispose();

            gate.Dispose();
        }
    }
}
