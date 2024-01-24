using DCL.Landscape.NoiseGeneration;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Composite Noise Data", fileName = "CompositeNoiseData", order = 0)]
    public class CompositeNoiseData : NoiseData
    {
        public List<NoiseData> add;
        public List<NoiseData> multiply;
        public List<NoiseData> subtract;

        public override INoiseGenerator GetGenerator(uint baseSeed, NoiseGeneratorCache cache) =>
            new CompositeNoiseGenerator(this, baseSeed, cache);
    }
}
