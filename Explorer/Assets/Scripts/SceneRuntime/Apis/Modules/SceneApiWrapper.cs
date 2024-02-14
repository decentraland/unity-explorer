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
        public GetSceneResponse GetSceneInfo()
        {
            URLAddress urlAddress;
            sceneData.TryGetMainScriptUrl(out urlAddress);

            return new GetSceneResponse()
            {
                BaseUrl = urlAddress,
                Metadata = "",
                Contents = {  },
                Cid = ""
            };
        }
    }
}
