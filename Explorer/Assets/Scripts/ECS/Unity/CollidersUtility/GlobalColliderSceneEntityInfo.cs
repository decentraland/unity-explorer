using SceneRunner.Scene;

namespace DCL.Interaction.Utility
{
    /// <summary>
    /// Used for detecting colliders in the scene world (like for example any interactable object of a scene).
    /// </summary>
    public readonly struct GlobalColliderSceneEntityInfo
    {
        public readonly SceneEcsExecutor EcsExecutor;
        public readonly ColliderSceneEntityInfo ColliderSceneEntityInfo;

        public GlobalColliderSceneEntityInfo(SceneEcsExecutor ecsExecutor, ColliderSceneEntityInfo colliderSceneEntityInfo)
        {
            EcsExecutor = ecsExecutor;
            ColliderSceneEntityInfo = colliderSceneEntityInfo;
        }
    }
}
