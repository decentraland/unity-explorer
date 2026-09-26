using CommunicationData.URLHelpers;
using DCL.Utility;
using DCL.Utility.Types;
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
            return ReadJson(jToken);
        }

        public static Profile.CompactInfo ReadJson(JToken jToken)
        {
            var jObject = jToken as JObject;

            string? rawUserId = jObject?["userId"]?.Value<string>() ?? jObject?["pointer"]?.Value<string>();
            Option<UserId> userId = UserId.New(rawUserId);

            if (!userId.Has)
                throw new ProfileParseException(rawUserId ?? "<missing userId>", jToken.ToString());

            bool hasClaimedName = jObject?["hasClaimedName"]?.Value<bool>() ?? false;
            string name = jObject?["name"]?.Value<string>() ?? "";
            var faceSnapshotUrl = URLAddress.FromString(jObject?["thumbnailUrl"]?.Value<string>() ?? "");

            Color? nameColor = jObject?["nameColor"] == null ? null : JsonUtils.DeserializeColor(jObject["nameColor"], Color.black);

            var compact = new Profile.CompactInfo(userId.Value, name, hasClaimedName, faceSnapshotUrl);
            string unclaimedName = jObject?["unclaimedName"]?.Value<string>() ?? "";
            compact.UnclaimedName = unclaimedName;

            if (nameColor.HasValue)
                compact.ClaimedNameColor = nameColor;

            return compact;
        }
    }
}
