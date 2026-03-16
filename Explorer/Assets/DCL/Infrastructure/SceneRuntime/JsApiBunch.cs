using System;
using System.Collections.Generic;

namespace SceneRuntime
{
    public class JsApiBunch : IDisposable
    {
        private readonly IJavaScriptEngine engine;
        private readonly ITypedArrayConverter typedArrayConverter;
        private readonly List<JsApiWrapper> wraps = new ();

        public JsApiBunch(IJavaScriptEngine engine, ITypedArrayConverter typedArrayConverter)
        {
            this.engine = engine;
            this.typedArrayConverter = typedArrayConverter;
        }

        public void Dispose()
        {
            foreach (JsApiWrapper wrap in wraps)
                wrap.Dispose();
        }

        public void AddHostObject<T>(string itemName, T target) where T: JsApiWrapper
        {
            target.engine = engine;
            target.TypedArrayConverter = typedArrayConverter;
            engine.AddHostObject(itemName, target);
            wraps.Add(target);
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            foreach (JsApiWrapper wrap in wraps)
                wrap.OnSceneIsCurrentChanged(isCurrent);
        }
    }
}
