using DCL.Landscape.NoiseGeneration;

namespace DCL.Landscape.Config
{
    public interface INoiseDataFactory
    {
        public INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
    }
}
