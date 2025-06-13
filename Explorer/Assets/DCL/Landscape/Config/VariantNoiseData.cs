using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(fileName = "VariantNoiseData", menuName = "DCL/Landscape/Variant Noise Data")]
    public class VariantNoiseData : NoiseDataBase
    {
        public uint seed;
        public NoiseDataBase other;
    }
}
