using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Conversion;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<Light> poolRegistry;
        private readonly ISceneStateProvider sceneStateProvider;

        public LightSourceSystem(World world,
            IComponentPool<Light> poolRegistry,
            ISceneStateProvider sceneStateProvider
        ) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            SetupLightSourceQuery(World);
            UpdateLightSourceStateQuery(World);
        }

        [Query]
        private void UpdateLightSourceState(ref LightSourceComponent lightSourceComponent, in PBLightSource pbLightSource)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None) return;
            if (!pbLightSource.IsDirty) return;

            Light lightSourceInstance = lightSourceComponent.lightSourceInstance;

            bool isSpot = pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot;

            lightSourceInstance.type = isSpot ? LightType.Spot : LightType.Point;

            lightSourceInstance.enabled = pbLightSource.Active;
            lightSourceInstance.color = PrimitivesConversionExtensions.PBColorToUnityColor(pbLightSource.Color);
            lightSourceInstance.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);
            lightSourceInstance.range = pbLightSource.Range;
            lightSourceInstance.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Shadow);

            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot)
            {
                lightSourceInstance.innerSpotAngle = pbLightSource.Spot.InnerAngle;
                lightSourceInstance.spotAngle = pbLightSource.Spot.OuterAngle;
            }
        }

        [Query]
        [None(typeof(LightSourceComponent))]
        private void SetupLightSource(in Entity entity, ref PBLightSource pbLightSource, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;
            if (pbLightSource.TypeCase == PBLightSource.TypeOneofCase.None) return;

            Light? lightSourceInstance = poolRegistry.Get();
            lightSourceInstance.transform.SetParent(transform.Transform);
            lightSourceInstance.transform.localPosition = Vector3.zero;
            lightSourceInstance.transform.localRotation = Quaternion.identity;

            lightSourceInstance.color = PrimitivesConversionExtensions.PBColorToUnityColor(pbLightSource.Color);
            lightSourceInstance.intensity = PrimitivesConversionExtensions.PBBrightnessInLumensToUnityCandels(pbLightSource.Brightness);
            lightSourceInstance.range = pbLightSource.Range;
            lightSourceInstance.shadows = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Shadow);

            bool isSpot = pbLightSource.TypeCase == PBLightSource.TypeOneofCase.Spot;

            lightSourceInstance.type = isSpot ? LightType.Spot : LightType.Point;

            if (isSpot)
            {
                lightSourceInstance.innerSpotAngle = pbLightSource.Spot.InnerAngle;
                lightSourceInstance.spotAngle = pbLightSource.Spot.OuterAngle;
            }

            lightSourceInstance.enabled = true;

            var lightSourceComponent = new LightSourceComponent(lightSourceInstance);

            World.Add(entity, lightSourceComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBLightSource))]
        private void HandleLightSourceRemoval(Entity entity, in LightSourceComponent component)
        {
            component.lightSourceInstance.enabled = false;
            poolRegistry.Release(component.lightSourceInstance);
            World.Remove<LightSourceComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleLightSourceEntityDestruction(in LightSourceComponent component)
        {
            component.lightSourceInstance.enabled = false;
            poolRegistry.Release(component.lightSourceInstance);
        }

        [Query]
        private void FinalizeLightSourceComponents(in LightSourceComponent lightSourceComponent)
        {
            poolRegistry.Release(lightSourceComponent.lightSourceInstance);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeLightSourceComponentsQuery(World);
        }
    }
}
