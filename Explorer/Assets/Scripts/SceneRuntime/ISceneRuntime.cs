using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using System;
using System.Threading.Tasks;

namespace SceneRuntime
{
    public interface ISceneRuntime : IDisposable
    {
        void RegisterEngineApi(IEngineApi api);

        UniTask StartScene();

        UniTask UpdateScene(float dt);
    }
}
