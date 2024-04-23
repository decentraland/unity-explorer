using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    public class ExtendedObjectPool<T> : ObjectPool<T>, IExtendedObjectPool<T> where T: class
    {
        private readonly List<T> list;
        private readonly Action<T> onDestroyAction;
        private readonly FieldInfo countAllField;

        public ExtendedObjectPool(
            Func<T> createFunc,
            Action<T> actionOnGet = null,
            Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null,
            bool collectionCheck = PoolConstants.CHECK_COLLECTIONS,
            int defaultCapacity = 10,
            int maxSize = 10000)
            : base(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck, defaultCapacity, maxSize)
        {
            onDestroyAction = actionOnDestroy;

            Type poolType = typeof(ObjectPool<T>);

            FieldInfo listField = poolType.GetField("m_List", BindingFlags.NonPublic | BindingFlags.Instance);
            list = listField?.GetValue(this) as List<T>;
            Assert.IsNotNull(list, "Couldn't find m_List field in Unity built-in ObjectPool<T> type. Pool ClearThrottled will not work.");

            countAllField = poolType.GetField("<CountAll>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(countAllField, "Couldn't find <CountAll>k__BackingField field in Unity built-in ObjectPool<T> type. Pool ClearThrottled will not work.");
        }

        public void ClearThrottled(int maxUnloadAmount)
        {
            int itemsToRemove = Math.Min(maxUnloadAmount, list.Count);
            if (itemsToRemove == 0) return;

            for (var i = 0; i < itemsToRemove; i++)
                onDestroyAction?.Invoke(list[i]);

            list.RemoveRange(0, itemsToRemove);

            // Can be optimized via unsafe approach: 'countAllField.SetValueDirect(__makeref(instance), newCountAll);' where 'instance = this' should be set in the constructor
            countAllField.SetValue(this, CountAll - itemsToRemove);
        }
    }
}
