using DCL.Landscape.NoiseGeneration;
using System;

namespace DCL.Landscape.Config
{
    public interface INoiseDataFactory
    {
        [Obsolete(TerrainModel.OBSOLESCENCE_MESSAGE)]
        public INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
    }
}
