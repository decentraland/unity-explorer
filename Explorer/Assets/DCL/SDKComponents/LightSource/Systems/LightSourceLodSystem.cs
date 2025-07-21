using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using JetBrains.Annotations;
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
    [UpdateAfter(typeof(LightSourceCullingSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceLodSystem : BaseUnityLoopSystem
    {
        private readonly LightSourceSettings settings;
        private readonly List<LodSettings> spotLightLods;
        private readonly List<LodSettings> pointLightLods;

        public LightSourceLodSystem(World world, LightSourceSettings settings, List<LodSettings> spotLightLods, List<LodSettings> pointLightLods) : base(world)
        {
            this.settings = settings;
            this.spotLightLods = spotLightLods;
            this.pointLightLods = pointLightLods;
        }

        protected override void Update(float t)
        {
            SelectLODQuery(World);
        }

        [Query]
        private void SelectLOD(in PBLightSource pbLightSource, ref LightSourceComponent lightSourceComponent)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource, settings.DefaultValues.Active)) return;

            if (!TryGetLodSettings(pbLightSource, out List<LodSettings> lodSettings)) return;

            lightSourceComponent.LOD = FindLOD(lodSettings, lightSourceComponent);

            ApplyLOD(ref lightSourceComponent, lodSettings[lightSourceComponent.LOD]);
        }

        private bool TryGetLodSettings(PBLightSource pbLightSource, out List<LodSettings> lodSettings)
        {
            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    lodSettings = spotLightLods;
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    lodSettings = pointLightLods;
                    break;

                default:
                    lodSettings = null;
                    return false;
            }

            return lodSettings.Count > 0;
        }

        private int FindLOD(List<LodSettings> lodSettings,  LightSourceComponent lightSourceComponent)
        {
            for (var lod = 0; lod < lodSettings.Count - 1; lod++)
                if (lightSourceComponent.DistanceToPlayer < lodSettings[lod + 1].Distance)
                    return lod;

            return lodSettings.Count - 1;
        }

        private void ApplyLOD(ref LightSourceComponent lightSourceComponent, LodSettings lodSetting)
        {
            if (lodSetting.IsCulled) lightSourceComponent.Culling |= LightSourceComponent.CullingFlags.CulledByLOD;

            Light light = lightSourceComponent.LightSourceInstance;

            light.shadows = LightSourceHelper.ClampShadowQuality(light.shadows, lodSetting.Shadows);

            // NOTE setting the resolution to 0 allows unity to decide on it (tiers are defined in the URP asset)
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
