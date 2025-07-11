using CommunicationData.URLHelpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace DCL.Communities
{
    [Serializable]
    public struct CommunityThumbnails : ISerializationCallbackReceiver
    {
        [JsonProperty("raw")]
        [CanBeNull] public Uri rawUri;

        public string raw;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            rawUri = raw.ToURL();
        }
    }
}
