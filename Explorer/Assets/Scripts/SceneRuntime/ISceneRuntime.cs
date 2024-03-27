using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using System;

namespace SceneRuntime
{
    public interface ISceneRuntime : IDisposable
    {
        void RegisterEngineApi(IEngineApi api);

        void RegisterRestrictedActionsApi(IRestrictedActionsAPI api);

        UniTask StartScene();

        UniTask UpdateScene(float dt);

        void ApplyStaticMessages(ReadOnlyMemory<byte> data);

        void SetIsDisposing();
    }
}
