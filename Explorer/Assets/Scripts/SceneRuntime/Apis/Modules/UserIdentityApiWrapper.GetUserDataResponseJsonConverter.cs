using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace SceneRuntime.Apis.Modules
{
    public partial class UserIdentityApiWrapper
    {
        private class GetUserDataResponseJsonConverter : JsonConverter<GetUserDataResponse>
        {
            public override void WriteJson(JsonWriter writer, GetUserDataResponse value, JsonSerializer serializer)
            {
                JObject root = new ();

                if (!value.data.HasValue)
                {
                    root.WriteTo(writer);
                    return;
                }

                var dataObj = new JObject();
                root[nameof(value.data)] = dataObj;
                GetUserDataResponse.Data data = value.data.Value;
                dataObj[nameof(data.version)] = data.version;
                dataObj[nameof(data.displayName)] = data.displayName;
                dataObj[nameof(data.hasConnectedWeb3)] = data.hasConnectedWeb3;
                dataObj[nameof(data.userId)] = data.userId;
                dataObj[nameof(data.publicKey)] = data.publicKey;

                var avatarObj = new JObject();
                dataObj[nameof(data.avatar)] = avatarObj;
                GetUserDataResponse.Data.Avatar avatar = data.avatar;
                avatarObj[nameof(avatar.bodyShape)] = avatar.bodyShape;
                avatarObj[nameof(avatar.eyeColor)] = avatar.eyeColor;
                avatarObj[nameof(avatar.hairColor)] = avatar.hairColor;
                avatarObj[nameof(avatar.skinColor)] = avatar.skinColor;
                var snapshotsObj = new JObject();
                avatarObj[nameof(avatar.snapshots)] = snapshotsObj;
                snapshotsObj[nameof(avatar.snapshots.face256)] = avatar.snapshots.face256;
                snapshotsObj[nameof(avatar.snapshots.body)] = avatar.snapshots.body;

                var wearablesArray = new JArray();
                avatarObj[nameof(avatar.wearables)] = wearablesArray;

                foreach (string w in avatar.wearables)
                    wearablesArray.Add(w);

                root.WriteTo(writer);
            }

            public override GetUserDataResponse ReadJson(JsonReader reader, Type objectType, GetUserDataResponse existingValue, bool hasExistingValue, JsonSerializer serializer) =>
                throw new NotImplementedException();
        }
    }
}
