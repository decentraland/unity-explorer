using DCL.Landscape.NoiseGeneration;
using System;
using UnityEngine;

namespace DCL.Landscape.Config
{
    public abstract class NoiseDataBase : ScriptableObject, INoiseDataFactory
    {
        [Obsolete(TerrainModel.OBSOLESCENCE_MESSAGE)]
        public abstract INoiseGenerator GetGenerator(uint baseSeed, uint variantSeed, NoiseGeneratorCache cache);
    }
}
