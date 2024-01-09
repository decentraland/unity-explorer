using Arch.Core;

namespace DCL.Utilities
{
    public class WorldProxy
    {
        private World world;

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

        public void Remove<T>(Entity entity)
        {
            world.Remove<T>(entity);
        }
    }
}
