using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public interface ITerrainBoundariesGenerator
    {
        Transform SpawnCliffs(int2 minInUnits, int2 maxInUnits, TerrainFactory factory, GameObject rootGo, int parcelSize);

        Transform SpawnBorderColliders(int2 minInUnits, int2 maxInUnits, int2 sidesLength, TerrainFactory factory, GameObject rootGo, int parcelSize);
    }
}
