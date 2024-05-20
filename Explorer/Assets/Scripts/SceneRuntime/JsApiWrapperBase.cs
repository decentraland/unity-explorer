using System;

namespace SceneRuntime
{
    public class JsApiWrapperBase<TApi> : IJsApiWrapper where TApi: IDisposable
    {
        protected readonly TApi api;

        protected JsApiWrapperBase(TApi api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();

            DisposeInternal();
        }

        protected virtual void DisposeInternal() { }
    }
}
