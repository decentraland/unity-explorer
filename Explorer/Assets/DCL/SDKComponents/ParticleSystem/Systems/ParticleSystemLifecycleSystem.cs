using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemLifecycleSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<UnityEngine.ParticleSystem> pool;
        private readonly IObjectPool<Material> materialPool;

        internal ParticleSystemLifecycleSystem(World world, ISceneStateProvider sceneStateProvider,
            IComponentPool<UnityEngine.ParticleSystem> pool, IObjectPool<Material> materialPool) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.pool = pool;
            this.materialPool = materialPool;
        }

        protected override void Update(float t)
        {
            CreateParticleSystemQuery(World);
            ReleaseRemovedParticleSystemQuery(World);
            ReleaseDestroyedParticleSystemQuery(World);
        }

        [Query]
        [None(typeof(ParticleSystemComponent))]
        private void CreateParticleSystem(in Entity entity, ref PBParticleSystem pbParticleSystem, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            UnityEngine.ParticleSystem psInstance = pool.Get();
            psInstance.transform.SetParent(transform.Transform, false);
            psInstance.transform.ResetLocalTRS();

            World.Add(entity, new ParticleSystemComponent(psInstance, psInstance.gameObject));
            pbParticleSystem.IsDirty = true;
        }

        [Query]
        [None(typeof(PBParticleSystem), typeof(DeleteEntityIntention))]
        private void ReleaseRemovedParticleSystem(in Entity entity, ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(World, entity, ref component);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void ReleaseDestroyedParticleSystem(in Entity entity, ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(World, entity, ref component);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeAllParticleSystemsQuery(World);
        }

        [Query]
        private void FinalizeAllParticleSystems(in Entity entity, ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(World, entity, ref component);
        }

        private void ReleaseParticleSystem(World world, in Entity entity, ref ParticleSystemComponent component)
        {
            component.CleanUpTexture(world);

            if (component.ParticleMaterial != null)
                materialPool.Release(component.ParticleMaterial);

            component.ParticleSystemInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            component.ParticleSystemInstance.transform.SetParent(null);
            pool.Release(component.ParticleSystemInstance);
        }
    }
}
