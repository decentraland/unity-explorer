using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;

namespace SceneRuntime
{
    public class JsApiBunch : IDisposable
    {
        private readonly V8ScriptEngine engine;
        private readonly List<IDisposable> wraps = new ();

        public JsApiBunch(V8ScriptEngine engine)
        {
            this.engine = engine;
        }

        public void AddHostObject<T>(string itemName, T target) where T: IDisposable
        {
            engine.AddHostObject(itemName, target);
            wraps.Add(target);
        }

        public void Dispose()
        {
            foreach (IDisposable wrap in wraps)
                wrap.Dispose();
        }
    }
}
