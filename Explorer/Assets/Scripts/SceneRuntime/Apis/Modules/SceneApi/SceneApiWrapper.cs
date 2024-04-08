using JetBrains.Annotations;
using System;

namespace SceneRuntime.Apis.Modules.SceneApi
{
    public class SceneApiWrapper : IJsApiWrapper
    {
        private readonly ISceneApi api;

        public SceneApiWrapper(ISceneApi api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public object GetSceneInfo() =>
            api.GetSceneInfo();
    }
}
