using System;
using UnityEngine.Rendering.Universal;

namespace DCL.Quality
{
    public interface IRendererFeaturesCache : IDisposable
    {
        T? GetRendererFeature<T>() where T: ScriptableRendererFeature;
    }
}
