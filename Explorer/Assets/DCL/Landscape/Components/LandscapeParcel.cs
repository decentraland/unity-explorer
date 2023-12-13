using Arch.Core;
using DCL.Landscape.Config;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace DCL.Landscape.Components
{
    public struct LandscapeParcel
    {
        public readonly Vector3 Position;
        public readonly Random Random;
        public readonly Dictionary<Transform, List<Transform>> Assets;

        public LandscapeParcel(Vector3 basePosition)
        {
            Position = basePosition;
            Random = new Random(Position.GetHashCode());
            Assets = new Dictionary<Transform, List<Transform>>();
        }
    }

    public struct LandscapeParcelInitialization { }

    public struct LandscapeParcelNoiseJob
    {
        public NativeArray<float> Results;
        public JobHandle Handle;
        public NativeArray<float2> OctaveOffsets;
        public float MaxPossibleHeight;
        public Vector3 ParcelPosition;
        public Entity Parcel;
        public LandscapeAsset LandscapeAsset;
    }
}
