using Arch.Core;
using UnityEngine.Profiling;

namespace ECS.Abstract
{
    /// <summary>
    ///     <inheritdoc cref="ISystem" />
    /// </summary>
    public abstract class BaseSystem : ISystem
    {
        protected readonly World world;

        protected BaseSystem(World world)
        {
            this.world = world;
        }

        public virtual void Initialize() { }

        public virtual void Dispose() { }

        public void Update()
        {
            Profiler.BeginSample($"{GetType()}.Update");
            UpdateInternal();
            Profiler.EndSample();
        }

        protected abstract void UpdateInternal();
    }
}
