using DCL.Landscape.Jobs;
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

    [CreateAssetMenu(fileName = "CompositeNoiseData", menuName = "DCL/Landscape/Composite Noise Data")]
    public class CompositeNoiseData : NoiseData
    {
        public List<CompositeOperation> operations;
        public List<SimpleOperation> simpleOperations;

        public float finalCutOff;
    }
}
