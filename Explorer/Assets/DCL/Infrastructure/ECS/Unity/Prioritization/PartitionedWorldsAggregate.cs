using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;

namespace ECS.Prioritization
{
    /// <summary>
    ///     Regulates worlds execution order based on partition data
    /// </summary>
    public class PartitionedWorldsAggregate : IPartitionedWorldsAggregate, IComparer<PartitionedWorldsAggregate.Entry>
    {
        private readonly List<Entry> entries = new (PoolConstants.SCENES_COUNT);

        public int Count => entries.Count;

        int IComparer<Entry>.Compare(Entry x, Entry y)
        {
            IPartitionComponent partitionX = x.data;
            IPartitionComponent partitionY = y.data;

            return BucketBasedComparer.INSTANCE.Compare(partitionX, partitionY);
        }

        public void Add(in IPartitionComponent data, SystemGroup systemGroup)
        {
            entries.Add(new Entry { data = data, systemGroup = systemGroup });
            UpdateSorting();
        }

        public void UpdateSorting()
        {
            entries.Sort(this);
        }

        public void TriggerUpdate()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                SystemGroup systemGroup = entries[i].systemGroup;
                systemGroup.Update();
            }
        }

        public void Remove(SystemGroup systemGroup)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].systemGroup == systemGroup)
                {
                    entries.RemoveAt(i);
                    return;
                }
            }
        }

        public class Factory : ISystemGroupAggregate<IPartitionComponent>.IFactory
        {
            public ISystemGroupAggregate<IPartitionComponent> Create(Type systemGroupType) =>
                new PartitionedWorldsAggregate();
        }

        internal struct Entry
        {
            internal IPartitionComponent data;
            internal SystemGroup systemGroup;
        }
    }
}
