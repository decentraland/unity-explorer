using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules;
using System;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    /// Unique instance for each Scene Runtime
    /// </summary>
    public class RuntimeImplementation : IRuntime
    {
        private readonly IJSOperations jsOperations;
        private readonly ISceneData sceneData;

        public RuntimeImplementation(IJSOperations jsOperations, ISceneData sceneData)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
        }

        public async UniTask<ITypedArray<byte>> ReadFile(string fileName)
        {
            sceneData.TryGetContentUrl(fileName, out string url);

            await UniTask.SwitchToMainThread(); // TODO(Mateo): I don't know how to do this better

            var request = UnityWebRequest.Get(url);

            await request.SendWebRequest();

            byte[] data = Encoding.ASCII.GetBytes(request.downloadHandler.text);

            await UniTask.SwitchToThreadPool(); // TODO(Mateo): I don't know how to do this better

            // create script byte array
            var array = jsOperations.CreateUint8Array(data.Length);

            // transfer data to script byte array
            array.Write(data, 0, Convert.ToUInt64(data.Length), 0);

            return array;
        }

        public void Dispose() { }
    }
}
