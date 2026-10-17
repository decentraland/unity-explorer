using Newtonsoft.Json;
using System;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    [Serializable]
    internal struct MuteRequestBody
    {
        [JsonProperty("muted_address")]
        public string MutedAddress;

        public MuteRequestBody(string mutedAddress)
        {
            MutedAddress = mutedAddress;
        }
    }
}
