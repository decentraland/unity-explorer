using CommunicationData.URLHelpers;
using CrdtEcsBridge.PoolsProviders;
using Decentraland.Kernel.Apis;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules
{
    public class SceneApiWrapper : IDisposable
    {
        private readonly ISceneData sceneData;

        public SceneApiWrapper(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public void Dispose()
        {
        }

        [UsedImplicitly]
        public object GetSceneInfo() =>
            new
            {
                BaseUrl = sceneData.SceneContentBaseUrl,
                Metadata = sceneData.SceneEntityDefinition.metadata, // parse metadata
                Contents = sceneData.SceneEntityDefinition.content, // pass contents array
                Cid = sceneData.SceneEntityDefinition.id
            };
    }
}
