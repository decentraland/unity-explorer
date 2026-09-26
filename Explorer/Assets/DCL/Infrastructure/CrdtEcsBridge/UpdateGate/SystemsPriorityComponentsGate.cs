using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using Google.Protobuf;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.UpdateGate
{
    public class SystemsPriorityComponentsGate : ISystemsUpdateGate
    {
        private const int PRIORITY_COMPONENTS_COUNT = 1; // As for now it is only SDKTransform
        private static readonly ThreadSafeHashSetPool<Type> POOL = new (PRIORITY_COMPONENTS_COUNT, PoolConstants.SCENES_COUNT);

        private HashSet<Type> priorityList = POOL.Get();

        public void Dispose()
        {
            if (priorityList != null)
            {
                POOL.Release(priorityList);
                priorityList = null;
            }
        }

        public void Open<T>() where T: IMessage
        {
            lock (priorityList) { priorityList.Add(typeof(T)); }
        }

        public bool IsOpen<T>() where T: IMessage
        {
            lock (priorityList) { return priorityList.Remove(typeof(T)); }
        }
    }
}
