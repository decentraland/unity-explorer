using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SpringBones
{
    /// <summary>
    /// Runtime spring bones tweak payload from builder. Mirrors
    /// decentraland/schemas src/dapps/preview/spring-bone-params.ts.
    /// </summary>
    [Serializable]
    public class SpringBonesParamsPayload
    {
        public string itemId;
        [JsonProperty("params")]
        public Dictionary<string, SpringBoneParamsDTO> parameters;

        public static SpringBonesParamsPayload Parse(string json) =>
            JsonConvert.DeserializeObject<SpringBonesParamsPayload>(json);
    }

    [Serializable]
    public class SpringBoneParamsDTO
    {
        public float stiffness;
        public float gravityPower;
        public float[] gravityDir;
        public float drag;
        public string center;
        public bool isRoot;
    }
}
