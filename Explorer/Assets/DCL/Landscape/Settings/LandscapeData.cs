using System;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public Action<float> OnDetailDistanceChanged;

        public bool showSatelliteView;
        public bool drawTerrain;
        public bool drawTerrainDetails;
        public Transform mapChunk;
        public TerrainGenerationData terrainData;
        public TerrainGenerationData worldsTerrainData;

        [SerializeField] private float detailDistanceValue = 200;
        public float DetailDistance
        {
            get => detailDistanceValue;

            set
            {
                if (Mathf.Approximately(detailDistanceValue, value))
                    return;

                detailDistanceValue = value;
                OnDetailDistanceChanged?.Invoke(value);
            }
        }
    }
}
