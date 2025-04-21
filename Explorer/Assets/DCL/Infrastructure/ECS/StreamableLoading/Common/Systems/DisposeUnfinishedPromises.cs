using Arch.Core;
using Arch.System;
using ECS.LifeCycle;
using ECS.StreamableLoading.Common.Components;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     If the promise is not consumed (entity is alive), its state may leak (along with the partial data) <br />
    ///     if it's handled on the side of the consumer it's still fine: <see cref="StreamableLoadingState" /> has a protection from being disposed of multiple times
    /// </summary>
    public partial class DisposeUnfinishedPromises : IFinalizeWorldSystem
    {
        private readonly World world;

        public DisposeUnfinishedPromises(World world)
        {
            this.world = world;
        }

        [Query]
        private void DisposeState(Entity entity, StreamableLoadingState state)
        {
            state.Dispose(world.Reference(entity));
        }

        public void FinalizeComponents(in Query query)
        {
            DisposeStateQuery(world);
        }
    }
}
