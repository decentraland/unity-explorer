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
        public class GetCommunityMembersResponseMemberDataConverter : JsonConverter<GetCommunityMembersResponse.MemberData>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, GetCommunityMembersResponse.MemberData? value, JsonSerializer serializer) =>
                throw new NotImplementedException();

            public override GetCommunityMembersResponse.MemberData? ReadJson(JsonReader reader, Type objectType, GetCommunityMembersResponse.MemberData? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JObject.Load(reader);

                GetCommunityMembersResponse.MemberData? instance = jObject.ToObject<GetCommunityMembersResponse.MemberData>();

                if (instance == null) return null;

                instance.Profile = new Profile.CompactInfo(
                    jObject["memberAddress"]!.Value<string>()!,
                    jObject["name"]!.Value<string>(),
                    jObject["hasClaimedName"]!.Value<bool>(),
                    jObject["profilePictureUrl"]!.Value<string>()!
                );

                return existingValue;
            }
        }

        [Preserve]
        public class CommunityInviteRequestDataConverter : JsonConverter<GetCommunityInviteRequestResponse.CommunityInviteRequestData>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, GetCommunityInviteRequestResponse.CommunityInviteRequestData? value, JsonSerializer serializer) =>
                throw new NotImplementedException();

            public override GetCommunityInviteRequestResponse.CommunityInviteRequestData? ReadJson(JsonReader reader, Type objectType, GetCommunityInviteRequestResponse.CommunityInviteRequestData? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JObject.Load(reader);

                GetCommunityInviteRequestResponse.CommunityInviteRequestData? instance = jObject.ToObject<GetCommunityInviteRequestResponse.CommunityInviteRequestData>();

                if (instance == null) return null;

                instance.Profile = new Profile.CompactInfo(
                    jObject["memberAddress"]!.Value<string>()!,
                    jObject["name"]!.Value<string>(),
                    jObject["hasClaimedName"]!.Value<bool>(),
                    jObject["profilePictureUrl"]!.Value<string>()!
                );

                return existingValue;
            }
        }
    }
}
