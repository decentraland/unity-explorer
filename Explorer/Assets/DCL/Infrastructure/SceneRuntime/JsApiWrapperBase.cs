using System;

namespace SceneRuntime
{
    public class JsApiWrapperBase<TApi> : IJsApiWrapper where TApi: IDisposable
    {
        protected readonly TApi api;
        protected readonly bool isLocalSceneDevelopment;

        protected JsApiWrapperBase(TApi api, bool isLocalSceneDevelopment)
        {
            this.api = api;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
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
