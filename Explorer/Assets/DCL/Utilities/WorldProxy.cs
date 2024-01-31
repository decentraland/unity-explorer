using Arch.Core;

namespace DCL.Utilities
{
    public class WorldProxy
    {
        private World? world;
        private Entity? mainPlayerEntity;

        public World? GetWorld() =>
            world;

        public Entity? GetMainPlayerEntity() =>
            mainPlayerEntity;

        public void SetWorld(World newWorld)
        {
            world = newWorld;
        }

        public void SetMainPlayerEntity(Entity playerEntity)
        {
            mainPlayerEntity = playerEntity;
        }

        public void Add<T>(Entity entity, in T component)
        {
            world?.Add(entity, component);
        }

        public void Set<T>(Entity entity, in T component)
        {
            world?.Set(entity, component);
        }

        public Entity? Create() =>
            world?.Create();

        public Entity? Create<T0, T1, T2>(
            in T0 t0Component = default,
            in T1 t1Component = default,
            in T2 t2Component = default) =>
            world?.Create(t0Component, t1Component, t2Component);

        public void Remove<T>(Entity entity)
        {
            world?.Remove<T>(entity);
        }

        public void Query<T0, T1>(in QueryDescription description, ForEach<T0, T1> forEach)
        {
            world?.Query(description, forEach);
        }
    }
}
