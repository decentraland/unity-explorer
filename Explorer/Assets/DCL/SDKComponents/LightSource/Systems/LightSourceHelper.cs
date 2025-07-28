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
    }
}
