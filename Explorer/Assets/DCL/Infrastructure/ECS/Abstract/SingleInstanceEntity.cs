using Arch.Core;
using Arch.Core.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ECS.Abstract
{
    /// <summary>
    ///     Locally cached entity that exists in one instance by design
    /// </summary>
    public readonly struct SingleInstanceEntity
    {
        private static readonly Entity[] TEMP = new Entity[1];

        private readonly Entity entity;

        public SingleInstanceEntity(in QueryDescription query, World world, bool strict = true)
        {
            TEMP[0] = Entity.Null;
            world.GetEntities(in query, TEMP);

            if (strict && TEMP[0].IsNull())
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

    public static class SingleInstanceEntityExtensions
    {
        public static Entity GetSingleInstanceEntityOrNull(this World world, in QueryDescription query)
        {
            return new SingleInstanceEntity(query, world, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull(this Entity entity)
        {
            return entity == Entity.Null;
        }
    }
}
