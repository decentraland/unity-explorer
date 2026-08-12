using DCL.Landscape.NoiseGeneration;

namespace DCL.Landscape.Config
{
    public interface INoiseDataFactory
    {
#pragma warning disable CS0618 // NoiseGeneratorCache preserved for World Terrain only
        public INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
#pragma warning restore CS0618
    }
}
