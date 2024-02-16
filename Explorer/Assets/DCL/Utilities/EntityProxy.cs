using Arch.Core;

namespace DCL.Utilities
{
    public class EntityProxy
    {
        public Entity? Entity { get; private set; }

        public void SetEntity(Entity entity)
        {
            Entity = entity;
        }
    }
}
