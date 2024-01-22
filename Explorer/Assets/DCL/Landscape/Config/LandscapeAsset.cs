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
        public float radius;

        /// <summary>
        ///     The more density, the more assets inside a chunk
        /// </summary>
        public float density;
        public ObjectRandomization randomization;
        public NoiseData noiseData;

    }
}
