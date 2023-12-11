using DCL.Landscape.Config;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class LandscapeData : ScriptableObject
    {
        // this determines the number of landscape nodes that the world has, increase this will increase the density of everything and will make the generation more slow
        // 16 = 16*16 = 256 nodes per parcel
        public int density = 16;
        public List<LandscapeAsset> assets;
    }
}
