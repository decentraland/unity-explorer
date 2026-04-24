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
        public float hitRadius;
        public bool isRoot;
    }
}
