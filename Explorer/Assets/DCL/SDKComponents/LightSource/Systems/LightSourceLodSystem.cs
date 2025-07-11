using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Handles selecting current LOD of each light source.
    /// Also updates properties that are LOD dependant.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourcePreCullingUpdateSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceLodSystem : BaseUnityLoopSystem
    {
        private List<LodSettings> spotLightsSettings;
        private List<LodSettings> pointLightsSettings;

        public LightSourceLodSystem(World world, List<LodSettings> spotLightsSettings, List<LodSettings> pointLightsSettings) : base(world)
        {
            this.spotLightsSettings = spotLightsSettings;
            this.pointLightsSettings = pointLightsSettings;
        }

        protected override void Update(float t)
        {
            SelectLODQuery(World);
        }

        [Query]
        private void SelectLOD(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource)) return;

            if (!TryGetLodSettings(pbLightSource, out List<LodSettings> lodSettings)) return;

            lightSourceComponent.LOD = FindLOD(lodSettings, lightSourceComponent);

            ApplyLOD(pbLightSource, ref lightSourceComponent, lodSettings[lightSourceComponent.LOD]);
        }

        private bool TryGetLodSettings(PBLightSource pbLightSource, out List<LodSettings> lodSettings)
        {
            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    lodSettings = spotLightsSettings;
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    lodSettings = pointLightsSettings;
                    break;

                default:
                    lodSettings = null;
                    return false;
            }

            return true;
        }

        private int FindLOD(List<LodSettings> lodSettings,  LightSourceComponent lightSourceComponent)
        {
            for (var lod = 0; lod < lodSettings.Count - 1; lod++)
                if (lightSourceComponent.DistanceToPlayer < lodSettings[lod + 1].Distance)
                    return lod;

            return lodSettings.Count - 1;
        }

        private void ApplyLOD(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent, LodSettings lodSetting)
        {
            if (lodSetting.IsCulled) lightSourceComponent.Culling |= LightSourceComponent.CullingFlags.CulledByLOD;

            Light light = lightSourceComponent.LightSourceInstance;

            light.shadows = LightSourceHelper.GetCappedUnityLightShadows(pbLightSource, lodSetting.Shadows);

            // NOTE setting the resolution to 0 allows unity to decide on the resolution (tiers are defined in the URP asset)
            light.shadowCustomResolution = lodSetting.OverrideShadowMapResolution ? lodSetting.ShadowMapResolution : 0;
        }

        [Serializable]
        public class LodSettings
        {
            public float Distance;

            public bool IsCulled;

            public LightShadows Shadows = LightShadows.None;

            public bool OverrideShadowMapResolution;

            public int ShadowMapResolution = 256;
        }
    }
}
