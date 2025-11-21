using CommunicationData.URLHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Profiles
{
    public class ProfileCompactInfoConverter : JsonConverter<Profile.CompactInfo>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Profile.CompactInfo value, JsonSerializer serializer) =>
            throw new NotImplementedException();

        public override Profile.CompactInfo ReadJson(JsonReader reader, Type objectType, Profile.CompactInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jToken = JToken.Load(reader);
            Profile.CompactInfo compactInfo = ReadJson(jToken);

            // TODO confirm format
            compactInfo.FaceSnapshotUrl = URLAddress.FromString(jToken["snapshots"]?["face256"]?.Value<string>() ?? "");
            return compactInfo;
        }

        public static Profile.CompactInfo ReadJson(JToken jObject)
        {
            var compact = new Profile.CompactInfo
            {
                UserId = jObject["userId"]?.Value<string>() ?? "",
                HasClaimedName = jObject["hasClaimedName"]?.Value<bool>() ?? false,
                Name = jObject["name"]?.Value<string>() ?? "",
                UnclaimedName = jObject["unclaimedName"]?.Value<string>() ?? "",
            };

            return compact;
        }
    }
}
