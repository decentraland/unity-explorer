using ECS.ComponentsPooling;
using System;
using UnityEngine;
using UnityEngine.Pool;

public class UnityComponentPool<T> : IComponentPool<T> where T: Component
{
    private readonly ObjectPool<T> unityComponentObjectPool;

    public UnityComponentPool(Func<T> onCreate = null, Action<T> onGet = null, Action<T> onRelease = null)
    {
        unityComponentObjectPool = new ObjectPool<T>(onCreate, actionOnGet: onGet, actionOnRelease: onRelease, defaultCapacity: 1000);
    }

    public T Get() =>
        unityComponentObjectPool.Get();

    public PooledObject<T> Get(out T v) =>
        unityComponentObjectPool.Get(out v);

    public void Release(T component) =>
        unityComponentObjectPool.Release(component);

    public void Clear() =>
        unityComponentObjectPool.Clear();

    public int CountInactive => unityComponentObjectPool.CountInactive;

    public void Dispose()
    {
        unityComponentObjectPool.Clear();
    }
}
