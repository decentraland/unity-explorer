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

        public void Dispose() { }

        public ISceneApi.GetSceneResponse GetSceneInfo() =>
            new (
                cid: sceneData.SceneEntityDefinition.id,
                contents: sceneData.SceneEntityDefinition.content ?? throw new InvalidOperationException("Scene content is null"),
                metadata: JsonConvert.SerializeObject(sceneData.SceneEntityDefinition.metadata),
                baseUrl: sceneData.SceneContent.ContentBaseUrl.Value
            );
    }
}
