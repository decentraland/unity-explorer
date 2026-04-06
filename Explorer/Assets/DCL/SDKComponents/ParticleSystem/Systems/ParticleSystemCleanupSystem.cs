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
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemCleanupSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<UnityEngine.ParticleSystem> pool;
        private readonly IObjectPool<Material> materialPool;

        internal ParticleSystemCleanupSystem(World world,
            IComponentPool<UnityEngine.ParticleSystem> pool,
            IObjectPool<Material> materialPool) : base(world)
        {
            this.pool = pool;
            this.materialPool = materialPool;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<ParticleSystemComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBParticleSystem), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(ref component);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(ref component);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeAllParticleSystemsQuery(World);
        }

        [Query]
        private void FinalizeAllParticleSystems(ref ParticleSystemComponent component)
        {
            ReleaseParticleSystem(ref component);
        }

        private void ReleaseParticleSystem(ref ParticleSystemComponent component)
        {
            component.CleanUpTexture(World);

            if (component.ParticleMaterial != null)
                materialPool.Release(component.ParticleMaterial);

            component.ParticleSystemInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            component.ParticleSystemInstance.transform.SetParent(null);
            pool.Release(component.ParticleSystemInstance);
        }
    }
}
