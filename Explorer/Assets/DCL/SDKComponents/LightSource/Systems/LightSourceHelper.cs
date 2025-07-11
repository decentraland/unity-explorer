using DCL.ECSComponents;
using System.Runtime.CompilerServices;

namespace DCL.SDKComponents.LightSource.Systems
{
    public static class LightSourceHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPBLightSourceActive(in PBLightSource pbLightSource)
        {
            return !pbLightSource.HasActive || pbLightSource.Active;
        }
    }
}
