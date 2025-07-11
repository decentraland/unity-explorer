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
        /// Gets the <see cref="LightShadows"/> value from the given PB light source, capped by the specified max value.
        /// Can be used to ensure shadow quality is not better than the desired value.
        /// </summary>
        public static LightShadows GetCappedUnityLightShadows(PBLightSource pbLightSource, LightShadows maxValue)
        {
            LightShadows preferredValue = LightShadows.None;

            switch (pbLightSource.TypeCase)
            {
                case PBLightSource.TypeOneofCase.Spot when pbLightSource.Spot.HasShadow:
                    preferredValue = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Spot.Shadow);
                    break;

                case PBLightSource.TypeOneofCase.Point when pbLightSource.Point.HasShadow:
                    preferredValue = PrimitivesConversionExtensions.PBLightSourceShadowToUnityLightShadow(pbLightSource.Point.Shadow);
                    break;
            }

            return (int)preferredValue <= (int)maxValue ? preferredValue : maxValue;
        }
    }
}
