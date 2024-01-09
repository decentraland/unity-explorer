using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering.Universal;

namespace Utility
{
    public static class ScriptableRenderingUtils
    {
        [CanBeNull]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FindRendererFeature<T>(this UniversalRendererData rendererData) where T: ScriptableRendererFeature =>
            (T)rendererData.rendererFeatures.Find(feature => feature is T);
    }
}
