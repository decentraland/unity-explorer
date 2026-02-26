using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct BatchedPointersIntentions : IDisposable
    {
        public readonly List<URN> Pointers;
        public IPartitionComponent Partition;

        private BatchedPointersIntentions(List<URN> pointers)
        {
            Pointers = pointers;
            Partition = PartitionComponent.MIN_PRIORITY;
        }

        public static BatchedPointersIntentions Create() =>
            new (WearableComponentsUtils.POINTERS_POOL.Get());

        public void Dispose()
        {
            WearableComponentsUtils.POINTERS_POOL.Release(Pointers);
        }
    }
}
