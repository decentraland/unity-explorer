using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Ipfs
{
    [Serializable]
    [JsonConverter(typeof(SceneParcelsConverter))]
    public class SceneMetadataScene
    {
        [field: NonSerialized]
        public Vector2Int DecodedBase { get; internal set; }

        [field: NonSerialized]
        public IReadOnlyList<Vector2Int> DecodedParcels { get; internal set; }
    }
}
