using SceneRunner.Scene;

namespace DCL.Interaction.Utility
{
    public readonly struct GlobalColliderSceneEntityInfo
    {
        public readonly SceneEcsExecutor EcsExecutor;
        public readonly ColliderEntityInfo ColliderEntityInfo;

        public GlobalColliderSceneEntityInfo(SceneEcsExecutor ecsExecutor, ColliderEntityInfo colliderEntityInfo)
        {
            EcsExecutor = ecsExecutor;
            ColliderEntityInfo = colliderEntityInfo;
        }
    }
}
