using Arch.Core;
using System;

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
            TEMP[0] = Entity.Null;
            world.GetEntities(in query, TEMP);

            if (TEMP[0] == Entity.Null)
                throw new Exception("Entity not found");

            entity = TEMP[0];
        }

        public static implicit operator Entity(SingleInstanceEntity singleInstanceEntity) =>
            singleInstanceEntity.entity;
    }
}
