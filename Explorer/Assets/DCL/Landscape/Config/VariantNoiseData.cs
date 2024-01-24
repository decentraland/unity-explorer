using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Variant Noise Data", fileName = "VariantNoiseData", order = 0)]
    public class VariantNoiseData : NoiseDataBase
    {
        public uint seed;
        public NoiseDataBase other;

        public override INoiseGenerator GetGenerator(uint baseSeed) =>
            other.GetGenerator(baseSeed + seed);
    }
}
