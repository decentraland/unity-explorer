using DCL.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.DTO
{
    [Serializable]
    public class SpringBonesDto
    {
        public const int SUPPORTED_VERSION = 1;

        public int version;
        public Dictionary<string, Dictionary<string, SpringBoneParamsDto>>? models;
    }

    [Serializable]
    public class SpringBoneParamsDto
    {
        public float stiffness;
        public float drag;

        [JsonConverter(typeof(Vector3JsonConverter))]
        public Vector3 gravityDir;

        public float gravityPower;

        // Reserved for future collider-based collision support; currently parsed but not consumed.
        public float hitRadius;

        public bool isRoot;
    }
}
