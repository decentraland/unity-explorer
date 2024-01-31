using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        [FormerlySerializedAs("showSatelliteView")] public bool disableSatelliteView;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
    }
}
