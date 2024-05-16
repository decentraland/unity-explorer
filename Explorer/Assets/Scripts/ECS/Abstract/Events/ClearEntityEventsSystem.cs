using Arch.Core;
using Arch.SystemGroups;
using ECS.Groups;
using System.Collections.Generic;

namespace ECS.Abstract
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ClearEntityEventsSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyList<EntityEventBuffer> eventBuffers;

        internal ClearEntityEventsSystem(World world, in EntityEventsBuilder builder) : base(world)
        {
            eventBuffers = builder.Build();
        }

        public override void Dispose()
        {
            for (var index = 0; index < eventBuffers.Count; index++)
            {
                EntityEventBuffer entityEventBuffer = eventBuffers[index];
                entityEventBuffer.Dispose();
            }
        }

        protected override void Update(float t)
        {
            for (var index = 0; index < eventBuffers.Count; index++)
            {
                EntityEventBuffer entityEventBuffer = eventBuffers[index];
                entityEventBuffer.Clear();
            }
        }
    }
}
