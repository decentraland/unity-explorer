using CommunicationData.URLHelpers;
using DCL.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

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

            compactInfo.FaceSnapshotUrl = URLAddress.FromString(jToken["thumbnailUrl"]?.Value<string>() ?? "");
            return compactInfo;
        }

        public static Profile.CompactInfo ReadJson(JToken jObject)
        {
            var userId = jObject["userId"]?.Value<string>() ?? jObject["pointer"]?.Value<string>() ?? "";
            var hasClaimedName = jObject["hasClaimedName"]?.Value<bool>() ?? false;
            var name = jObject["name"]?.Value<string>() ?? "";
            var faceSnapshotUrl = URLAddress.FromString(jObject["thumbnailUrl"]?.Value<string>() ?? "");
            Color? nameColor =  jObject["nameColor"] == null ? null : JsonUtils.DeserializeColor(jObject["nameColor"], Color.black);

            var compact = new Profile.CompactInfo(userId, name, hasClaimedName, faceSnapshotUrl);
            var unclaimedName = jObject["unclaimedName"]?.Value<string>() ?? "";
            compact.UnclaimedName = unclaimedName;
            if(nameColor.HasValue)
                compact.ClaimedNameColor = nameColor;

            return compact;
        }
    }
}
