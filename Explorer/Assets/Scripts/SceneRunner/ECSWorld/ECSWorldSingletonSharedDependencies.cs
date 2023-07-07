using ECS.ComponentsPooling;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
        }
    }
}
