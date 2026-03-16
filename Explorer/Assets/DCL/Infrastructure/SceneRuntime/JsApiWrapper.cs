using System;
using System.Threading;

namespace SceneRuntime
{
    public abstract class JsApiWrapper : IDisposable
    {
        internal readonly CancellationTokenSource disposeCts;
        internal IJavaScriptEngine? engine;
        internal ITypedArrayConverter TypedArrayConverter { get; set; } = null!;

        protected JsApiWrapper(CancellationTokenSource disposeCts, IJavaScriptEngine? engine = null)
        {
            this.disposeCts = disposeCts;
            this.engine = engine;
        }

        public virtual void Dispose() { }

        public virtual void OnSceneIsCurrentChanged(bool isCurrent) { }
    }

    public class JsApiWrapper<TApi> : JsApiWrapper where TApi: IDisposable
    {
        public readonly TApi api;

        protected JsApiWrapper(TApi api, CancellationTokenSource disposeCts, IJavaScriptEngine? engine = null) : base(disposeCts, engine)
        {
            this.api = api;
        }

        public sealed override void Dispose()
        {
            api.Dispose();

            DisposeInternal();
        }

        protected virtual void DisposeInternal() { }
    }
}
