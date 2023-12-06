using SceneRunner.Scene;

namespace DCL.Interaction.Utility
{
    public readonly struct GlobalColliderEntityInfo
    {
        public readonly SceneEcsExecutor EcsExecutor;
        public readonly ColliderEntityInfo ColliderEntityInfo;

        public GlobalColliderEntityInfo(SceneEcsExecutor ecsExecutor, ColliderEntityInfo colliderEntityInfo)
        {
            EcsExecutor = ecsExecutor;
            ColliderEntityInfo = colliderEntityInfo;
        }
    }
}
