using DCL.Landscape.NoiseGeneration;
using UnityEngine;

namespace DCL.Landscape.Config
{
    public abstract class NoiseDataBase : ScriptableObject, INoiseDataFactory
    {
#pragma warning disable CS0618 // NoiseGeneratorCache preserved for World Terrain only
        public abstract INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
#pragma warning restore CS0618
    }
}
