using System;
using System.Collections.Generic;

namespace SceneRuntime
{
    /// <summary>
    ///     Owns and lifecycle-manages the set of <see cref="JsApiWrapper" /> instances that back a single scene's
    ///     JavaScript API surface. On construction each wrapper is injected with the shared <see cref="IJavaScriptEngine" />
    ///     and <see cref="ITypedArrayConverter" />, then registered as a host object so JS code can call its methods.
    ///     <para>
    ///         <see cref="Dispose" /> tears down all wrappers together, and
    ///         <see cref="OnSceneIsCurrentChanged" /> propagates scene-focus transitions to every registered API.
    ///     </para>
    /// </summary>
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
