using DCL.Utilities;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.Donations
{
    public class DonationsService : IDisposable
    {
        public IReadonlyReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> DonationsEnabledCurrentScene => donationsEnabledCurrentScene;
        private readonly ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)> donationsEnabledCurrentScene = new ((false, null, null));

        private readonly IScenesCache scenesCache;

        public DonationsService(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
            scenesCache.CurrentScene.OnUpdate += OnCurrentSceneChanged;
        }

        public void Dispose()
        {
            scenesCache.CurrentScene.OnUpdate -= OnCurrentSceneChanged;
        }

        private void OnCurrentSceneChanged(ISceneFacade? currentScene)
        {
            string? creatorAddress = currentScene?.SceneData.GetCreatorAddress();
            donationsEnabledCurrentScene.UpdateValue((creatorAddress != null, creatorAddress, currentScene?.Info.BaseParcel));
        }
    }
}
