using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    public static class LightSourceHelper
    {
        /// <summary>
        /// Whether the given PB light source should be considered active.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPBLightSourceActive(in PBLightSource pbLightSource, bool activeByDefault = true)
        {
            return pbLightSource.HasActive ? pbLightSource.Active : activeByDefault;
        }

        /// <summary>
        /// Clamps the value of <paramref name="quality"/> to <see cref="maxQuality"/>.
        /// </summary>
        public static LightShadows ClampShadowQuality(LightShadows quality, LightShadows maxQuality)
        {
            return (int)quality <= (int)maxQuality ? quality : maxQuality;
        }

        /// <summary>
        /// Gets the non-empty LOD settings list that corresponds to the light source type.
        /// </summary>
        public static bool TryGetLodSettings(LightSourceSettings settings, PBLightSource.TypeOneofCase typeCase, out List<LightSourceSettings.LodSettings> lodSettings)
        {
            switch (typeCase)
            {
                case PBLightSource.TypeOneofCase.Spot:
                    lodSettings = settings.SpotLightsLods;
                    break;

                case PBLightSource.TypeOneofCase.Point:
                    lodSettings = settings.PointLightsLods;
                    break;

                default:
                    lodSettings = null;
                    return false;
            }

            return lodSettings.Count > 0;
        }

        /// <summary>
        /// Finds the LOD index for the given squared distance to the player.
        /// Returns -1 when the distance is beyond the last LOD, meaning the light is culled.
        /// </summary>
        public static int FindLOD(List<LightSourceSettings.LodSettings> lodSettings, float distanceToPlayerSq)
        {
            for (var lod = 0; lod < lodSettings.Count; lod++)
            {
                float distance = lodSettings[lod].Distance;

                if (distanceToPlayerSq < distance * distance)
                    return lod;
            }

            return -1;
        }
    }
}
