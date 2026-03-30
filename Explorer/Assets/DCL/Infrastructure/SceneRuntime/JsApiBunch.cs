using System;
using System.Collections.Generic;

#if !UNITY_WEBGL || (UNITY_EDITOR && !EDITOR_DEBUG_WEBGL)
using Microsoft.ClearScript.V8;
#endif

namespace SceneRuntime
{
    /// <summary>
    ///     Owns and lifecycle-manages the set of <see cref="JsApiWrapper" /> instances that back a single scene's
    ///     JavaScript API surface. On construction each wrapper is registered as a host object so JS code can call
    ///     its methods. On WebGL, wrappers are also injected with the shared <see cref="IJavaScriptEngine" /> and
    ///     <see cref="ITypedArrayConverter" />.
    ///     <para>
    ///         <see cref="Dispose" /> tears down all wrappers together, and
    ///         <see cref="OnSceneIsCurrentChanged" /> propagates scene-focus transitions to every registered API.
    ///     </para>
    /// </summary>
    public class JsApiBunch : IDisposable
    {
        private readonly List<JsApiWrapper> wraps = new ();

#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
        private readonly IJavaScriptEngine engine;
        private readonly ITypedArrayConverter typedArrayConverter;

        public JsApiBunch(IJavaScriptEngine engine, ITypedArrayConverter typedArrayConverter)
        {
            this.engine = engine;
            this.typedArrayConverter = typedArrayConverter;
        }
#else
        private readonly V8ScriptEngine engine;

        public JsApiBunch(V8ScriptEngine engine)
        {
            this.engine = engine;
        }
#endif

        public void Dispose()
        {
            foreach (JsApiWrapper wrap in wraps)
                wrap.Dispose();
        }

        public void AddHostObject<T>(string itemName, T target) where T: JsApiWrapper
        {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
            target.engine = engine;
            target.TypedArrayConverter = typedArrayConverter;
#endif
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
