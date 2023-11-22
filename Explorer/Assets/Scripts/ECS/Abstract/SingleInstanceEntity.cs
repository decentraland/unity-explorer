using Arch.Core;
using Arch.Core.Utils;
using System;
using System.Linq;

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
            {
                throw new Exception($"Entity not found for Query All: {Format(query.All)} Any: {Format(query.Any)} None: {Format(query.None)} Exclusive: {Format(query.Exclusive)}");

                string Format(ComponentType[] componentTypes)
                {
                    var formattedString = string.Join(", ", componentTypes.Select(ct => ct.Type));
                    return "[" + formattedString + "]";
                }
            }

            entity = TEMP[0];
        }

        public static implicit operator Entity(SingleInstanceEntity singleInstanceEntity) =>
            singleInstanceEntity.entity;




    }
}
