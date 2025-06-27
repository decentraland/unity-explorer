using System;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [Serializable]
    public class ParcelData : ScriptableObject
    {
        public int2[] ownedParcels;
        public int2[] emptyParcels;
    }
}
