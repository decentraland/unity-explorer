using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.SceneApi;
using System;

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
                metadata: JsonConvert.SerializeObject(sceneData.SceneEntityDefinition.metadata),
                baseUrl: sceneData.SceneContent.ContentBaseUrl.Value
            );
    }
}
