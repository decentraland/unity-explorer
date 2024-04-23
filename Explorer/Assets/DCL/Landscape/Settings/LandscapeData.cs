using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public bool showSatelliteView;
        public bool drawTerrain;
        public bool drawTerrainDetails;
        public float detailDistance = 200;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
        public TerrainGenerationData worldsTerrainData;
    }
}
