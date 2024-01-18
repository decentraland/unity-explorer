using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Composite Noise Data", fileName = "CompositeNoiseData", order = 0)]
    public class CompositeNoiseData : ScriptableObject
    {
        public NoiseData baseData;
        public List<NoiseSettings> add;
        public List<NoiseSettings> multiply;
        public List<NoiseSettings> subtract;
    }
}
