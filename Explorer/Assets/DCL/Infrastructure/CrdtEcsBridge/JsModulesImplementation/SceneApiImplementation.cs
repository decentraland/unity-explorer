using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.SceneApi;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SceneApiImplementation : ISceneApi
    {
        private readonly ISceneData sceneData;

        public SceneApiImplementation(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public ISceneApi.GetSceneResponse GetSceneInfo() =>
            new (
                cid: sceneData.SceneEntityDefinition.id,
                contents: sceneData.SceneEntityDefinition.content,
                metadata: sceneData.SceneEntityDefinition.metadata.OriginalJson,
                baseUrl: sceneData.SceneContent.ContentBaseUrl.Value
            );
    }
}
