using DCL.Landscape.NoiseGeneration;
using System;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(fileName = "VariantNoiseData", menuName = "DCL/Landscape/Variant Noise Data")]
    public class VariantNoiseData : NoiseDataBase
    {
        public uint seed;
        public NoiseDataBase other;

        [Obsolete(TerrainModel.OBSOLESCENCE_MESSAGE)]
        public override INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache) =>
            other.GetGenerator(baseSeed, seed, cache);
    }
}
