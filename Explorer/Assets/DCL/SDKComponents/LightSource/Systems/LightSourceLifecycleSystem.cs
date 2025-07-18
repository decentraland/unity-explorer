using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;

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

        public LightSourceLifecycleSystem(World world,
            ISceneStateProvider sceneStateProvider,
            IComponentPool<Light> poolRegistry
        ) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.poolRegistry = poolRegistry;
        }

        protected override void Update(float t)
        {
            CreateLightSourceComponentQuery(World);
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
            lightSourceInstance.intensity = 0;

            lightSourceInstance.transform.localScale = Vector3.one;
            lightSourceInstance.transform.SetParent(transform.Transform, false);

            float intensity = PrimitivesConversionExtensions.PBIntensityInLumensToUnityCandels(pbLightSource.Intensity);
            var lightSourceComponent = new LightSourceComponent(lightSourceInstance, intensity);
            World.Add(entity, lightSourceComponent);

            pbLightSource.IsDirty = true;
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseLightSourcesQuery(World);
        }

        [Query]
        private void ReleaseLightSources(in LightSourceComponent lightSourceComponent)
        {
            poolRegistry.Release(lightSourceComponent.LightSourceInstance);
        }
    }
}
