using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public bool showSatelliteView;
        public bool drawTerrain;
        public bool drawTerrainDetails;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
    }
}
