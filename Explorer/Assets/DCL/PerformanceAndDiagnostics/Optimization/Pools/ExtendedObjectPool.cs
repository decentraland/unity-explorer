using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace DCL.PerformanceAndDiagnostics.Optimization.Pools
{
    /// <summary>
    ///     Extension of Unity built-in ObjectPool for throttled clearing
    /// </summary>
    public class ExtendedObjectPool<T> : ObjectPool<T> where T: class
    {
        private readonly List<T> list;
        private readonly Action<T> onDestroyAction;
        private readonly FieldInfo countAllReflected;

        public ExtendedObjectPool(
            Func<T> createFunc,
            Action<T> actionOnGet = null,
            Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null,
            bool collectionCheck = true,
            int defaultCapacity = 10,
            int maxSize = 10000)
            : base(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck, defaultCapacity, maxSize)
        {
            onDestroyAction = actionOnDestroy;

            Type poolType = typeof(ObjectPool<T>);
            FieldInfo listField = poolType.GetField("m_List", BindingFlags.NonPublic | BindingFlags.Instance);
            list = listField?.GetValue(this) as List<T>;
            Assert.IsNotNull(list, "Couldn't find m_List field in Unity built-in ObjectPool<T> type");

            countAllReflected = poolType.GetField("<CountAll>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(countAllReflected, "Couldn't find <CountAll>k__BackingField field in Unity built-in ObjectPool<T> type");
        }

        public void ClearThrottled(int maxUnloadAmount)
        {
            int itemsToRemove = Math.Min(maxUnloadAmount, list.Count);

            for (var i = 0; i < itemsToRemove; i++)
                onDestroyAction?.Invoke(list[i]);

            list.RemoveRange(0, itemsToRemove);

            int newCountAll = CountAll - itemsToRemove;
            countAllReflected.SetValue(this, newCountAll);
        }
    }
}
