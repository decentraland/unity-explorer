using JetBrains.Annotations;
using System.Threading;

namespace SceneRuntime.Apis.Modules.SceneApi
{
    public class SceneApiWrapper : JsApiWrapper
    {
        private readonly ISceneApi api;

        public SceneApiWrapper(ISceneApi api, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.api = api;
        }

        [UsedImplicitly]
        public object GetSceneInfo() =>
            api.GetSceneInfo();

        public override void Dispose() { }
    }
}
