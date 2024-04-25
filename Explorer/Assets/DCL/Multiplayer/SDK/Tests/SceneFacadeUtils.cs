using Arch.Core;
using DCL.Diagnostics;
using NSubstitute;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.SDK.Tests
{
    public class SceneFacadeUtils
    {
        public static ISceneFacade CreateSceneFacadeSubstitute(Vector2Int baseCoords, World sceneWorld)
        {
            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            var sceneShortInfo = new SceneShortInfo(baseCoords, "fake-scene");
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(sceneShortInfo);
            sceneFacade.Info.Returns(sceneShortInfo);
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            var sceneEcsExecutor = new SceneEcsExecutor(sceneWorld, new MutexSync());
            sceneFacade.EcsExecutor.Returns(sceneEcsExecutor);
            return sceneFacade;
        }
    }
}
