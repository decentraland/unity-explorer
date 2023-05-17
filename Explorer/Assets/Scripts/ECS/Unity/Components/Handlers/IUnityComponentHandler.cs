using ECS.ComponentsPooling;
using System;
using UnityEngine;

namespace ECS.Unity.Components
{
    public interface IUnityComponentHandler
    {
        Transform parentContainer { get; }

        IComponentPool GetPool();

        Type GetComponentType();
    }

    public interface IUnityComponentHandler<T> : IUnityComponentHandler where T: Component
    {
        T HandleCreation();

        void HandleGet(T obj);

        void HandleRelease(T obj);

        IComponentPool IUnityComponentHandler.GetPool() =>
            new UnityComponentPool<T>(HandleCreation, HandleGet, HandleRelease);

        Type IUnityComponentHandler.GetComponentType() =>
            typeof(T);
    }
}
