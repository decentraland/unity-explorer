using Arch.Core;

namespace ECS.Abstract
{
    /// <summary>
    ///     Locally cached entity that exists in one instance by design
    /// </summary>
    public readonly struct SingleInstanceEntity
    {
        private static readonly Entity[] TEMP = new Entity[1];

        private readonly Entity entity;

        public SingleInstanceEntity(in QueryDescription query, World world)
        {
            world.GetEntities(in query, TEMP);
            entity = TEMP[0];
        }

        public static implicit operator Entity(SingleInstanceEntity singleInstanceEntity) =>
            singleInstanceEntity.entity;
    }
}
