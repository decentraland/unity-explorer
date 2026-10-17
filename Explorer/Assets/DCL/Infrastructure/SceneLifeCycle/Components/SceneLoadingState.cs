using System;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingState
    {
        public bool PromiseCreated;
        public bool FullQuality;
        public VisualSceneState VisualSceneState;

        public static SceneLoadingState CreateRoad() =>
            new ()
            {
                PromiseCreated = false,
                FullQuality = true,
                VisualSceneState = VisualSceneState.Road,
            };

        public static SceneLoadingState CreatePortableExperience() =>
            new ()
            {
                PromiseCreated = true,
                FullQuality = true,
                VisualSceneState = VisualSceneState.ShowingScene,
            };

        //Testing purpose
        public static SceneLoadingState CreateBuiltScene() =>
            new ()
            {
                PromiseCreated = true,
                FullQuality = true,
                VisualSceneState = VisualSceneState.ShowingScene,
            };

        //Testing purpose
        public static SceneLoadingState CreateHighQualityLOD() =>
            new ()
            {
                PromiseCreated = true,
                FullQuality = true,
                VisualSceneState = VisualSceneState.ShowingLod,
            };
    }

    public enum VisualSceneState
    {
        Uninitialized,
        ShowingScene,
        ShowingLod,
        Road,
    }
}
