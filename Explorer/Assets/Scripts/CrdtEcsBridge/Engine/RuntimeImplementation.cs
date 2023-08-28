using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
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
        private readonly ISceneData sceneData;

        public RuntimeImplementation(ISceneData sceneData)
        {
            this.sceneData = sceneData;
        }

        public async UniTask<ScriptableByteArray> ReadFile(string fileName)
        {
            sceneData.TryGetContentUrl(fileName, out string url);

            await UniTask.SwitchToMainThread(); // TODO(Mateo): I don't know how to do this better
            var request = UnityWebRequest.Get(url);

            await request.SendWebRequest();

            byte[] bytes = Encoding.ASCII.GetBytes(request.downloadHandler.text);

            var result = new ScriptableByteArray(new ArraySegment<byte>(bytes));
            await UniTask.SwitchToThreadPool();

            return result;
        }

        public void Dispose() { }
    }
}
