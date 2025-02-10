using System;

namespace SceneRuntime
{
    public class JsApiWrapperBase<TApi> : IJsApiWrapper where TApi: IDisposable
    {
        protected readonly TApi api;
        protected bool isPreview;

        protected JsApiWrapperBase(TApi api, bool isPreview)
        {
            this.api = api;
            this.isPreview = isPreview;
        }

        public void Dispose()
        {
            api.Dispose();

            DisposeInternal();
        }

        public virtual void OnSceneIsCurrentChanged(bool isCurrent) { }

        protected virtual void DisposeInternal() { }
    }
}
