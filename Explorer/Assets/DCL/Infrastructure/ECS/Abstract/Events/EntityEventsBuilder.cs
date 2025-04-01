using System;
using System.Collections.Generic;

namespace ECS.Abstract
{
    public class EntityEventsBuilder
    {
        private List<EntityEventBuffer> buffers;

        public EntityEventBuffer<T> Rent<T>()
        {
            buffers ??= new List<EntityEventBuffer>(10);

            for (var i = 0; i < buffers.Count; i++)
            {
                if (buffers[i] is EntityEventBuffer<T> existingBuffer)
                    return existingBuffer;
            }

            var buffer = EntityEventBuffer<T>.Rent();
            buffers.Add(buffer);
            return buffer;
        }

        internal IReadOnlyList<EntityEventBuffer> Build() =>
            (IReadOnlyList<EntityEventBuffer>) buffers ?? Array.Empty<EntityEventBuffer>();
    }
}
