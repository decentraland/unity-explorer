using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Landscape Asset", fileName = "LandscapeAsset", order = 0)]
    public class LandscapeAsset : ScriptableObject
    {
        public GameObject asset;

        /// <summary>
        ///     This radius is used by the placement system, to avoid overlapping with nearby owned scenes
        /// </summary>
        public NoiseDataBase noiseData;

        /// <summary>
        ///     The more density, the more assets inside a chunk
        /// </summary>
        [Header("Settings when used as tree")]
        public ObjectRandomization randomization;
        public float radius;

        [Header("Settings when used as detail")]
        public TerrainDetailSettings TerrainDetailSettings;
    }
}
