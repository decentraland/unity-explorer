using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
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
        public static bool IsPBLightSourceActive(in PBLightSource pbLightSource)
        {
            return !pbLightSource.HasActive || pbLightSource.Active;
        }

        /// <summary>
        /// Clamps the value of <paramref name="quality"/> to <see cref="maxQuality"/>.
        /// </summary>
        public static LightShadows ClampShadowQuality(LightShadows quality, LightShadows maxQuality)
        {
            return (int)quality <= (int)maxQuality ? quality : maxQuality;
        }

        /// <summary>
        /// Clamps the shadow quality from the given <see cref="PBLightSource"/> to <paramref name="maxQuality"/>.
        /// </summary>
        public static LightShadows ClampShadowQuality(PBLightSource pbLightSource, LightShadows maxQuality)
        {
            return ClampShadowQuality(GetShadowQualityFromPBLightSource(pbLightSource), maxQuality);
        }

        public static LightShadows GetShadowQualityFromPBLightSource(PBLightSource pbLightSource)
        {
            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot when pbLightSource.Spot.HasShadow:
                    return PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Spot.Shadow);

                case PBLightSource.TypeOneofCase.Point when pbLightSource.Point.HasShadow:
                    return PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Point.Shadow);
            }

            return LightShadows.None;
        }
    }
}
