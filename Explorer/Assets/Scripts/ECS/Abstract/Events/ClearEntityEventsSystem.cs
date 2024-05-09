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
            foreach (EntityEventBuffer entityEventBuffer in eventBuffers)
                entityEventBuffer.Dispose();
        }

        protected override void Update(float t)
        {
            foreach (EntityEventBuffer entityEventBuffer in eventBuffers)
                entityEventBuffer.Clear();
        }
    }
}
