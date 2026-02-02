using Arch.Core;
using ECS.LifeCycle;
using SceneRunner.Scene;

namespace ECS.Unity.SceneBoundsChecker
{
    public class SceneBoundsChecker : ISceneIsCurrentListener
    {
        private readonly World world;
        private readonly ISceneStateProvider sceneStateProvider;

        public SceneBoundsChecker(World world, ISceneStateProvider sceneStateProvider)
        {
            this.world = world;
            this.sceneStateProvider = sceneStateProvider;
        }

        void ISceneIsCurrentListener.OnSceneIsCurrentChanged(bool value)
        {
            if (sceneStateProvider.State.Value() == SceneState.Disposed)
                return;

            if (value)
            {
                ActivatePrimitiveCollidersQuery(World);
                ActivateGltfCollidersQuery(World);
            }
            else
            {
                DeactivatePrimitiveCollidersQuery(World);
                DeactivateGltfCollidersQuery(World);
            }
        }

        private struct Gurr : IForEach<PrimitiveColliderComponent>
    }
}
