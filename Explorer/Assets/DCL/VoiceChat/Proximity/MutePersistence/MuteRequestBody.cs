using Newtonsoft.Json;
using System;

namespace DCL.VoiceChat.MutePersistence
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
