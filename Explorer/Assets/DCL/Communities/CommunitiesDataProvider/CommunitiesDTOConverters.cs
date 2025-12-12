using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.Scripting;

namespace DCL.Communities.CommunitiesDataProvider
{
    /// <summary>
    ///     Contains temporary converters that aim at bridging between the server data and <see cref="Profile.CompactInfo" />
    /// </summary>
    [Obsolete(IProfileRepository.PROFILE_FRAGMENTATION_OBSOLESCENCE)]
    public static class CommunitiesDTOConverters
    {
        [Preserve]
        public class FriendsInCommunityConverter : JsonConverter<Profile.CompactInfo[]>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, Profile.CompactInfo[]? value, JsonSerializer serializer) =>
                throw new NotImplementedException();

            public override Profile.CompactInfo[]? ReadJson(JsonReader reader, Type objectType, Profile.CompactInfo[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return Array.Empty<Profile.CompactInfo>();

                var array = JArray.Load(reader);

                var profiles = new Profile.CompactInfo[array.Count];

                for (int i = 0; i < array.Count; i++)
                {
                    var token = array[i] as JObject;
                    if (token == null) continue;

                    profiles[i] = ReadElement(token);
                }

                return profiles;

                Profile.CompactInfo ReadElement(JObject token) =>
                    new (
                        token["address"]!.Value<string>()!,
                        token["name"]!.Value<string>()!,
                        token["hasClaimedName"]!.Value<bool>(),
                        token["profilePictureUrl"]!.Value<string>()!
                    );
            }
        }

        [Preserve]
        public class GetCommunityMembersResponseMemberDataConverter : JsonConverter<GetCommunityMembersResponse.MemberData>
        {
            private static readonly JsonSerializer DEFAULT_SERIALIZER = new ();

            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, GetCommunityMembersResponse.MemberData? value, JsonSerializer serializer) =>
                throw new NotImplementedException();

            public override GetCommunityMembersResponse.MemberData? ReadJson(JsonReader reader, Type objectType, GetCommunityMembersResponse.MemberData? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JObject.Load(reader);

                // Create instance first, then populate to avoid triggering converter recursion
                GetCommunityMembersResponse.MemberData instance = existingValue ?? new GetCommunityMembersResponse.MemberData();

                using (JsonReader jObjectReader = jObject.CreateReader()) { DEFAULT_SERIALIZER.Populate(jObjectReader, instance); }

                instance.Profile = new Profile.CompactInfo(
                    jObject["memberAddress"]!.Value<string>()!,
                    jObject["name"]!.Value<string>(),
                    jObject["hasClaimedName"]!.Value<bool>(),
                    jObject["profilePictureUrl"]!.Value<string>()!
                );

                return instance;
            }
        }

        [Preserve]
        public class CommunityInviteRequestDataConverter : JsonConverter<GetCommunityInviteRequestResponse.CommunityInviteRequestData>
        {
            private static readonly JsonSerializer DEFAULT_SERIALIZER = new ();

            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, GetCommunityInviteRequestResponse.CommunityInviteRequestData? value, JsonSerializer serializer) =>
                throw new NotImplementedException();

            public override GetCommunityInviteRequestResponse.CommunityInviteRequestData? ReadJson(JsonReader reader, Type objectType, GetCommunityInviteRequestResponse.CommunityInviteRequestData? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JObject.Load(reader);

                // Create instance first, then populate to avoid triggering converter recursion
                GetCommunityInviteRequestResponse.CommunityInviteRequestData instance = existingValue ?? new GetCommunityInviteRequestResponse.CommunityInviteRequestData();

                using (JsonReader jObjectReader = jObject.CreateReader()) { DEFAULT_SERIALIZER.Populate(jObjectReader, instance); }

                instance.Profile = new Profile.CompactInfo(
                    jObject["memberAddress"]!.Value<string>()!,
                    jObject["name"]!.Value<string>(),
                    jObject["hasClaimedName"]!.Value<bool>(),
                    jObject["profilePictureUrl"]!.Value<string>()!
                );

                return instance;
            }
        }
    }
}
