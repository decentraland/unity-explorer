#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEBUG
#endif

using System.Diagnostics;
using UnityEngine;
using Utility.Multithreading;

namespace Utility
{
    public static class UnityObjectExtensions
    {
        [Conditional("DEBUG")]
        public static void SetDebugName(this Object clip, string name)
        {
            clip.name = $"{name} : {MultithreadingUtility.FrameCount}";
        }
    }
}
