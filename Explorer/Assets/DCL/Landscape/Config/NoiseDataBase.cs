using DCL.Landscape.NoiseGeneration;
using UnityEngine;

namespace DCL.Landscape.Config
{
    public abstract class NoiseDataBase : ScriptableObject, INoiseDataFactory
    {
        public abstract INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
    }
}
