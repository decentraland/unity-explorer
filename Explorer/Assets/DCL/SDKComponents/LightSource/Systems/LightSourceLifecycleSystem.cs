using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Creates and releases light sources.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceLifecycleSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<Light> poolRegistry;

        public LightSourceLifecycleSystem(World world, ISceneStateProvider sceneStateProvider, IComponentPool<Light> poolRegistry) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.poolRegistry = poolRegistry;
        }

        protected override void Update(float t)
        {
            CreateLightSourceComponentQuery(World);
            ReleaseLightSourceRemovedFromSceneQuery(World);
            ReleaseDestroyedLightSourceQuery(World);
        }

        [Query]
        [None(typeof(LightSourceComponent))]
        private void CreateLightSourceComponent(in Entity entity, ref PBLightSource pbLightSource, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None)
            {
                ReportHub.LogWarning(GetReportCategory(), "Scene attempted to create a light source with type None");
                return;
            }

            Light lightSourceInstance = poolRegistry.Get();
            lightSourceInstance.transform.SetParent(transform.Transform, false);
            lightSourceInstance.transform.ResetLocalTRS();

            var lightSourceComponent = new LightSourceComponent(lightSourceInstance);
            World.Add(entity, lightSourceComponent);

            pbLightSource.IsDirty = true;
        }

        [Query]
        [None(typeof(PBLightSource), typeof(DeleteEntityIntention))]
        private void ReleaseLightSourceRemovedFromScene(in LightSourceComponent lightSourceComponent)
        {
            ReleaseLightSource(lightSourceComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void ReleaseDestroyedLightSource(in LightSourceComponent lightSourceComponent)
        {
            ReleaseLightSource(lightSourceComponent);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseLightSourceQuery(World);
        }

        [Query]
        private void ReleaseLightSource(in LightSourceComponent lightSourceComponent)
        {
            poolRegistry.Release(lightSourceComponent.LightSourceInstance);
        }
    }
}
