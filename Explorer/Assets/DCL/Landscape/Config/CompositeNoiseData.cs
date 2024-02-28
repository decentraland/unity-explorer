using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [Serializable]
    public struct CompositeOperation
    {
        public NoiseJobOperation operation;
        public NoiseData noise;
        public bool disable;
    }

    [Serializable]
    public struct SimpleOperation
    {
        public NoiseJobOperation operation;
        public float value;
        public bool disable;
    }

    [CreateAssetMenu(menuName = "Landscape/Composite Noise Data", fileName = "CompositeNoiseData", order = 0)]
    public class CompositeNoiseData : NoiseData
    {
        public List<CompositeOperation> operations;
        public List<SimpleOperation> simpleOperations;

        public float finalCutOff;

        public override INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache) =>
            new CompositeNoiseGenerator(this, baseSeed, variantSeed, cache);
    }
}
