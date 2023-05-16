using UnityEngine;

public interface IUnityComponentHandler<T> where T: Component
{
    T HandleCreation();

    void HandleGet(T obj);

    void HandleRelease(T obj);

    UnityComponentPool<T> GetPool();
}
