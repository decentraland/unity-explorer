#nullable enable

using Newtonsoft.Json;
using System;
using RichTypes;

namespace DCL.SceneBannedUsers
{
    [Serializable]
    public struct SceneRoomMetadata
    {
        [JsonProperty("bannedAddresses")] public string[] BannedAddresses;
        [JsonProperty("sceneAdmins")] public string[] SceneAdmins;

        public static Result<SceneRoomMetadata> FromJson(string rawJson)
        {
            try
            {
                SceneRoomMetadata usersRoomMetadata = JsonConvert.DeserializeObject<SceneRoomMetadata>(rawJson);
                return Result<SceneRoomMetadata>.SuccessResult(usersRoomMetadata);
            }
            catch (Exception e)
            {
                return Result<SceneRoomMetadata>.ErrorResult($"Cannot parse SceneRoomMetadata from: '{rawJson}'; error: {e.Message ?? "unknown error"}");
            }
        }
    }
}
