using System;
using System.Threading;

namespace SceneRuntime
{
    public abstract class JsApiWrapper : IDisposable
    {
        internal readonly CancellationTokenSource disposeCts;

        protected JsApiWrapper(CancellationTokenSource disposeCts)
        {
            this.disposeCts = disposeCts;
        }

        public virtual void Dispose() { }

        public virtual void OnSceneIsCurrentChanged(bool isCurrent) { }
    }

    public class JsApiWrapper<TApi> : JsApiWrapper where TApi: IDisposable
    {
        internal readonly TApi api;

        protected JsApiWrapper(TApi api, CancellationTokenSource disposeCts) : base(disposeCts)
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
