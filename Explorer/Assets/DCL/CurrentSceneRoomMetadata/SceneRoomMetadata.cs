using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace DCL.SceneBannedUsers
{
    [Serializable]
    public struct SceneRoomMetadata
    {
        [JsonProperty("bannedAddresses")] public string[] BannedAddresses;
        [JsonProperty("sceneAdmins")] public string[] SceneAdmins;
    }
}
