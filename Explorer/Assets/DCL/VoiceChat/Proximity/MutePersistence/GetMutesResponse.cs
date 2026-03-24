using Newtonsoft.Json;
using System;

namespace DCL.VoiceChat.MutePersistence
{
    [Serializable]
    internal struct GetMutesResponse
    {
        [JsonProperty("data")]
        public MutesData Data;

        [Serializable]
        internal struct MutesData
        {
            [JsonProperty("results")]
            public MutedUserEntry[] Results;

            [JsonProperty("total")]
            public int Total;

            [JsonProperty("limit")]
            public int Limit;

            [JsonProperty("offset")]
            public int Offset;
        }

        [Serializable]
        internal struct MutedUserEntry
        {
            [JsonProperty("address")]
            public string Address;
        }
    }
}
