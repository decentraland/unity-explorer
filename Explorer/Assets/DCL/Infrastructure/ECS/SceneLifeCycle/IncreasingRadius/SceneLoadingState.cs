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
                VisualSceneState = VisualSceneState.ROAD,
            };

        public static SceneLoadingState CreatePortableExperience() =>
            new ()
            {
                PromiseCreated = true,
                FullQuality = true,
                VisualSceneState = VisualSceneState.SHOWING_SCENE,
            };
    }

    public enum VisualSceneState
    {
        UNINITIALIZED,
        SHOWING_SCENE,
        SHOWING_LOD,
        ROAD,
    }
}
