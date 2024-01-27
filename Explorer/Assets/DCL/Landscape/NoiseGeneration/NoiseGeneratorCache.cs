using DCL.Landscape.Config;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace DCL.Landscape.NoiseGeneration
{
    public class NoiseGeneratorCache : IDisposable
    {
        private readonly Dictionary<INoiseDataFactory, INoiseGenerator> cachedGenerators = new ();

        public INoiseGenerator GetGeneratorFor(INoiseDataFactory noiseData, uint baseSeed)
        {
            Assert.IsNotNull(noiseData, "Noise data is null, check the terrain generation data");

            if (cachedGenerators.TryGetValue(noiseData, out INoiseGenerator noiseGen))
                return noiseGen;

            INoiseGenerator generator = noiseData.GetGenerator(baseSeed, 0, this);
            cachedGenerators.Add(noiseData, generator);

            return cachedGenerators[noiseData];
        }

        public void Dispose()
        {
            foreach (KeyValuePair<INoiseDataFactory, INoiseGenerator> cachedGenerator in cachedGenerators)
                cachedGenerator.Value.Dispose();
        }
    }
}
