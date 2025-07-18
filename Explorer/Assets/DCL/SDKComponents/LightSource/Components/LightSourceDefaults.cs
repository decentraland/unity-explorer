using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.SDKComponents.LightSource
{
    [CreateAssetMenu(fileName = "LightSourceDefaults", menuName = "DCL/LightSource/LightSource Default Settings", order = 1)]
    [Serializable]
    public class LightSourceDefaults : ScriptableObject
    {
        // I'll update the url when the protocol PR is merged
        [Header("These values come from the protocol definition \nfound here: https://github.com/decentraland/protocol/pull/234")]
        public bool active;
        public Color color;
        public float brightness;
        public float range;

        [Header("Point Settings")]
        public bool pointShadows;

        [Header("Spot Settings")]
        public bool spotShadows;
        public float innerAngle;
        public float outerAngle;
    }
}
