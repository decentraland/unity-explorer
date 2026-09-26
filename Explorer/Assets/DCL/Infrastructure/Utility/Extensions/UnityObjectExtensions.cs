#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEBUG
#endif

using Arch.Core;
using System.Diagnostics;
using UnityEngine;
using Utility.Multithreading;

namespace Utility
{
    public static class UnityObjectExtensions
    {
        [Conditional("DEBUG")]
        public static void SetDebugName(this Object @object, string name)
        {
            @object.name = $"{name} : {MultithreadingUtility.FrameCount}";
        }

        [Conditional("DEBUG")]
        public static void SetDebugName(this Object @object, Entity entity, string sdkName)
        {
            @object.name = $"Entity {entity.Id} ({sdkName}) : T={MultithreadingUtility.FrameCount}";
        }
    }
}
