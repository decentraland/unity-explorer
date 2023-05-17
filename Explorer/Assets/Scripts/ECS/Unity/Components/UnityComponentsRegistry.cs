using ECS.ComponentsPooling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Components
{
    public class UnityComponentsRegistry
    {
        private readonly Transform rootContainer;
        public readonly Dictionary<Type, IComponentPool> unityComponents = new (30);

        public UnityComponentsRegistry()
        {
            rootContainer = new GameObject("POOLS_CONTAINER").transform;
        }

        public UnityComponentsRegistry Add(IUnityComponentHandler unityComponentHandler)
        {
            unityComponentHandler.parentContainer.SetParent(rootContainer);
            unityComponents.Add(unityComponentHandler.GetComponentType(), unityComponentHandler.GetPool());
            return this;
        }
    }
}
