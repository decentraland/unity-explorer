using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime
{
    public class JsApiBunch : IDisposable
    {
        private readonly V8ScriptEngine engine;
        private readonly List<JsApiWrapper> wraps = new ();

        public JsApiBunch(V8ScriptEngine engine)
        {
            this.engine = engine;
        }

        public void AddHostObject<T>(string itemName, T target) where T: JsApiWrapper
        {
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
        }
    }
}
