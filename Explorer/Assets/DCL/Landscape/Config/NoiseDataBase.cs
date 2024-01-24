using System;
using UnityEngine;

namespace DCL.Landscape.Config
{
    public abstract class NoiseDataBase : ScriptableObject, INoiseDataFactory
    {
        public abstract INoiseGenerator GetGenerator(uint baseSeed);
    }
}
