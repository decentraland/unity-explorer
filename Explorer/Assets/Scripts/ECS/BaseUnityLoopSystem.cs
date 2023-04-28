using Arch.Core;
using Arch.System;
using UnityEngine.Profiling;

namespace ECS
{
    /// <summary>
    /// Provides additional functionality to `BaseSystem`
    /// </summary>
    public abstract class BaseUnityLoopSystem : BaseSystem<World, float>
    {
        protected BaseUnityLoopSystem(World world) : base(world) { }

        public sealed override void Update(in float t)
        {
            Profiler.BeginSample($"{GetType()}.Update");
            Update(t);
            Profiler.EndSample();
        }

        protected abstract void Update(float t);
    }
}
