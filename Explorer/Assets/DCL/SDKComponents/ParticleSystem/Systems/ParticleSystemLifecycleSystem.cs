using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using Utility;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(ParticleSystemGroup))]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemLifecycleSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<UnityEngine.ParticleSystem> pool;

        internal ParticleSystemLifecycleSystem(World world, ISceneStateProvider sceneStateProvider,
            IComponentPool<UnityEngine.ParticleSystem> pool) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.pool = pool;
        }

        protected override void Update(float t)
        {
            CreateParticleSystemQuery(World);
        }

        [Query]
        [None(typeof(ParticleSystemComponent))]
        private void CreateParticleSystem(in Entity entity, ref PBParticleSystem pbParticleSystem, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            UnityEngine.ParticleSystem psInstance = pool.Get();
            psInstance.transform.SetParent(transform.Transform, false);
            psInstance.transform.ResetLocalTRS();

            pbParticleSystem.IsDirty = true;
            World.Add(entity, new ParticleSystemComponent(psInstance, psInstance.gameObject));
        }
    }
}
