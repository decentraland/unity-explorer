using ECS.ComponentsPooling;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityComponentsRegistry
{
    private readonly GameObject parentContainer;
    public readonly Dictionary<Type, IComponentPool> unityComponentsPool = new (30);

    public UnityComponentsRegistry()
    {
        parentContainer = new GameObject("POOLS_CONTAINER");
        unityComponentsPool.Add(typeof(Transform), new UnityTransformHandler(parentContainer.transform).GetPool());
    }
}
