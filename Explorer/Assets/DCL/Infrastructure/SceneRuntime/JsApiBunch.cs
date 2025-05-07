using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;

namespace SceneRuntime
{
    public class JsApiBunch : IDisposable
    {
        private readonly V8ScriptEngine engine;
        private readonly List<IJsApiWrapper> wraps = new ();

        public JsApiBunch(V8ScriptEngine engine)
        {
            this.engine = engine;
        }

        public void AddHostObject<T>(string itemName, T target) where T: IJsApiWrapper
        {
            engine.AddHostObject(itemName, target);
            wraps.Add(target);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            foreach (IJsApiWrapper wrap in wraps)
                wrap.OnSceneIsCurrentChanged(isCurrent);
        }

        public void Dispose()
        {
            foreach (IJsApiWrapper wrap in wraps)
                wrap.Dispose();
        }

        public void SetIsDisposing()
        {
            foreach (IJsApiWrapper wrap in wraps)
                wrap.SetIsDisposing();
        }
    }
}
