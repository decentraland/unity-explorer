using SceneRunner.Scene;

namespace DCL.Interaction.Utility
{
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
