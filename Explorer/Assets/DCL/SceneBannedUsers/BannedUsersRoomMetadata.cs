using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace DCL.SceneBannedUsers
{
    [Serializable]
    public struct BannedUsersRoomMetadata
    {
        [JsonProperty("bannedAddresses")] public string[] BannedAddresses;
    }
}
