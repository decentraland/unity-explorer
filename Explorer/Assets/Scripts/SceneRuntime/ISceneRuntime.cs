using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using System;

namespace SceneRuntime
{
    public interface ISceneRuntime : IDisposable
    {
        void RegisterEngineApi(IEngineApi api);

        UniTask StartScene();

        UniTask UpdateScene(float dt);

        void ApplyStaticMessages(ReadOnlyMemory<byte> data);

        void SetIsDisposing();
    }
}
