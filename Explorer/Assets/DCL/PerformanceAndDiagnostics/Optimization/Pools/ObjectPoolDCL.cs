using System;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Optimization.Pools
{
    /// <summary>
    ///     DCL replication of Unity object pooling codebase
    /// </summary>
    public class ObjectPoolDCL<T> : IDisposable, IObjectPoolDCL<T> where T: class
    {
        private readonly List<T> list;
        private readonly Func<T> createFunc;
        private readonly Action<T> actionOnGet;
        private readonly Action<T> actionOnRelease;
        private readonly Action<T> actionOnDestroy;
        private readonly int maxSize;
        private readonly bool collectionCheck;

        public int CountAll { get; private set; }

        public int CountActive => CountAll - CountInactive;
        public int CountInactive => list.Count;

        public ObjectPoolDCL(
            Func<T> createFunc,
            Action<T> actionOnGet = null,
            Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null,
            bool collectionCheck = true,
            int defaultCapacity = 10,
            int maxSize = 10000)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max Size must be greater than 0", nameof(maxSize));

            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));

            list = new List<T>(defaultCapacity);
            this.maxSize = maxSize;
            this.collectionCheck = collectionCheck;

            this.actionOnGet = actionOnGet;
            this.actionOnRelease = actionOnRelease;
            this.actionOnDestroy = actionOnDestroy;
        }

        public void Dispose() =>
            Clear();

        public T Get()
        {
            T obj;

            if (list.Count == 0)
            {
                obj = createFunc();
                ++CountAll;
            }
            else
            {
                int index = list.Count - 1;
                obj = list[index];
                list.RemoveAt(index);
            }

            actionOnGet?.Invoke(obj);

            return obj;
        }

        public PooledObjectDCL<T> Get(out T v) =>
            new (v = Get(), this);

        public void Release(T element)
        {
            if (collectionCheck && list.Count > 0)
                foreach (T elementInList in list)
                    if (element == elementInList)
                        throw new InvalidOperationException("Trying to release an object that has already been released to the pool.");

            actionOnRelease?.Invoke(element);

            if (CountInactive < maxSize)
                list.Add(element);
            else
                actionOnDestroy?.Invoke(element);
        }

        public void Clear()
        {
            if (actionOnDestroy != null)
                foreach (T obj in list)
                    actionOnDestroy(obj);

            list.Clear();
            CountAll = 0;
        }

        public void Clear(int maxChunkSize)
        {
            var removedAmount = 0;

            int maxAmount = Math.Min(maxChunkSize, list.Count);

            // Iterate backward to safely remove items from the list
            for (int index = maxAmount - 1; index >= 0; index--, removedAmount++)
            {
                T obj = list[index];
                actionOnDestroy?.Invoke(obj);

                list.RemoveAt(index);
            }

            CountAll -= removedAmount;
        }
    }
}
