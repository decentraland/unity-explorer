using Arch.Core;

namespace DCL.Utilities
{
    public class WorldProxy
    {
        private World world;

        public World GetWorld()
        {
            return world;
        }

        public void SetWorld(World newWorld)
        {
            world = newWorld;
        }

        public void Add(Entity entity, in object cmp)
        {
            world.Add(entity, cmp);
        }

        public Entity Create()
        {
            return world.Create();
        }

        public Entity Create<T0,T1,T2>(
            in T0 t0Component = default(T0),
            in T1 t1Component = default(T1),
            in T2 t2Component = default(T2))
        {
            return world.Create(t0Component, t1Component, t2Component);
        }

        public void Remove<T>(Entity entity)
        {
            world.Remove<T>(entity);
        }
    }
}
