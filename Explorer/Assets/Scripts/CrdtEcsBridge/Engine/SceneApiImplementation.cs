using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using System.Threading;

namespace CrdtEcsBridge.Engine
{
    public class SceneApiImplementation : ISceneApi
    {
        private readonly ISceneData sceneData;

        public SceneApiImplementation(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public void Dispose() { }

        public async UniTask<ISceneApi.GetSceneResponse> GetSceneInfoAsync(CancellationToken ct) =>
            new ()
            {
                baseUrl = sceneData.SceneContent.ContentBaseUrl.Value,
                contents = sceneData.SceneEntityDefinition.content,
                cid = sceneData.SceneEntityDefinition.id,
                metadata = JsonConvert.SerializeObject(sceneData.SceneEntityDefinition.metadata),
            };
    }
}
