using Arch.Core;
using Arch.System;
using UnityEngine.Profiling;

namespace ECS.Abstract
{
    /// <summary>
    /// Provides additional functionality to `BaseSystem`
    /// </summary>
    public abstract class BaseUnityLoopSystem : BaseSystem<World, float>
    {
        private readonly CustomSampler updateSampler;

        protected BaseUnityLoopSystem(World world) : base(world)
        {
            updateSampler = CustomSampler.Create($"{GetType().Name}.Update");
        }

        public sealed override void Update(in float t)
        {
            updateSampler.Begin();
            Update(t);
            updateSampler.End();
        }

        protected abstract void Update(float t);
    }
}
