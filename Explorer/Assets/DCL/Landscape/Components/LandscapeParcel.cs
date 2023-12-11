using DCL.Landscape.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = System.Random;

namespace DCL.Landscape.Components
{
    public struct LandscapeParcel
    {
        public readonly Vector3 Position;
        public readonly Random Random;

        public LandscapeParcel(Vector3 basePosition)
        {
            Position = basePosition;
            Random = new Random(Position.GetHashCode());
        }
    }

    public struct LandscapeParcelInitialization { }

    public struct LandscapeParcelNoiseJob
    {
        public NativeArray<float> Results;
        public JobHandle Handle;
    }
}
