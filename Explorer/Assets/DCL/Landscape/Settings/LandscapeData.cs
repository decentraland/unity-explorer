using DCL.Landscape.Config;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        public int density = 16;
        public NoiseData TreeNoiseData;
        public Transform debugParcelObject;
        public Transform debugSubEntityObject;
    }
}
