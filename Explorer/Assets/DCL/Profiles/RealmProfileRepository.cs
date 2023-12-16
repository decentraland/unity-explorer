using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS;
using Ipfs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public class RealmProfileRepository : IProfileRepository
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly IProfileCache profileCache;
        private readonly URLBuilder urlBuilder = new ();

        public RealmProfileRepository(IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
        }

        public async UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id)) return null;

            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            urlBuilder.AppendDomain(ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"))
                      .AppendParameter(new URLParameter("version", version.ToString()));

            URLAddress url = urlBuilder.Build();

            try
            {
                GenericGetRequest response = await webRequestController.GetAsync(new CommonArguments(url), ct);

                using GetProfileJsonRootDto root = await response.CreateFromNewtonsoftJsonAsync<GenericGetRequest, GetProfileJsonRootDto>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, text, exception),
                    converters: new ProfileJsonRootDtoConverter());

                if (root.avatars == null) return null;
                if (root.avatars.Count == 0) return null;

                // TODO: probable responsibility issues thus we might not want to affect the cache
                // but avoids extra allocations in case the profile already exists
                Profile profile = profileCache.Get(id) ?? new Profile();
                root.avatars[0].CopyTo(profile);
                profileCache.Set(id, profile);

                return profile;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == 404)
                    return null;

                throw;
            }
        }

        private class ProfileJsonRootDtoConverter : JsonConverter<GetProfileJsonRootDto>
        {
            public override void WriteJson(JsonWriter writer, GetProfileJsonRootDto? value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override GetProfileJsonRootDto? ReadJson(JsonReader reader, Type objectType, GetProfileJsonRootDto? existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JObject.Load(reader);
                existingValue ??= GetProfileJsonRootDto.Create();
                DeserializeProfileList(jObject["avatars"], ref existingValue.avatars);
                return existingValue;
            }

            private void DeserializeProfileList(JToken? root, ref List<ProfileJsonDto>? list)
            {
                if (root is { Type: JTokenType.Array })
                {
                    list ??= new List<ProfileJsonDto>();
                    list.Clear();

                    foreach (JToken? item in root.Children())
                        list.Add(DeserializeProfile(item, ProfileJsonDto.Create()));
                }
                else
                    list?.Clear();
            }

            private ProfileJsonDto DeserializeProfile(JToken? jObject, ProfileJsonDto profile)
            {
                if (jObject == null) return profile;

                profile.hasClaimedName = jObject["hasClaimedName"]?.Value<bool>() ?? false;
                profile.description = jObject["description"]?.Value<string>() ?? "";
                profile.tutorialStep = jObject["tutorialStep"]?.Value<int>() ?? 0;
                profile.name = jObject["name"]?.Value<string>() ?? "";
                profile.userId = jObject["userId"]?.Value<string>() ?? "";
                profile.email = jObject["email"]?.Value<string>() ?? "";
                profile.ethAddress = jObject["ethAddress"]?.Value<string>() ?? "";
                profile.version = jObject["version"]?.Value<int>() ?? 0;
                profile.unclaimedName = jObject["unclaimedName"]?.Value<string>() ?? "";
                profile.hasConnectedWeb3 = jObject["hasConnectedWeb3"]?.Value<bool>() ?? false;
                profile.avatar = DeserializeAvatar(jObject["avatar"], profile.avatar);
                DeserializeArrayToList(jObject["blocked"], ref profile.blocked);
                DeserializeArrayToList(jObject["interests"], ref profile.interests);

                return profile;
            }

            private AvatarJsonDto DeserializeAvatar(JToken? jObject, AvatarJsonDto? avatar)
            {
                avatar ??= new AvatarJsonDto();
                avatar.eyes ??= new EyesJsonDto();
                avatar.eyes.color = DeserializeColor(jObject["eyes"]?["color"], new AvatarColorJsonDto());
                avatar.hair ??= new HairJsonDto();
                avatar.hair.color = DeserializeColor(jObject["hair"]?["color"], new AvatarColorJsonDto());
                avatar.skin ??= new SkinJsonDto();
                avatar.skin.color = DeserializeColor(jObject["skin"]?["color"], new AvatarColorJsonDto());

                avatar.bodyShape = jObject["bodyShape"]?.Value<string>() ?? "";

                DeserializeArrayToList(jObject["wearables"], ref avatar.wearables);
                DeserializeEmoteList(jObject["emotes"], ref avatar.emotes);

                avatar.snapshots ??= new AvatarSnapshotJsonDto();
                avatar.snapshots.face256 = jObject["snapshots"]?["face256"]?.Value<string>() ?? "";
                avatar.snapshots.body = jObject["snapshots"]?["body"]?.Value<string>() ?? "";

                DeserializeArrayToList(jObject["forceRender"], ref avatar.forceRender);

                return avatar;
            }

            private void DeserializeEmoteList(JToken? root, ref List<EmoteJsonDto>? list)
            {
                if (root is { Type: JTokenType.Array })
                {
                    list ??= new List<EmoteJsonDto>();
                    list.Clear();

                    foreach (JToken? item in root.Children())
                        list.Add(DeserializeEmote(item, new EmoteJsonDto()));
                }
                else
                    list?.Clear();
            }

            private EmoteJsonDto DeserializeEmote(JToken item, EmoteJsonDto emote)
            {
                emote.slot = item["slot"]?.Value<int>() ?? 0;
                emote.urn = item["urn"]?.Value<string>() ?? "";
                return emote;
            }

            private AvatarColorJsonDto? DeserializeColor(JToken? jObject, AvatarColorJsonDto color)
            {
                if (jObject == null) return null;

                color.r = jObject["r"]?.Value<float>() ?? 0;
                color.g = jObject["g"]?.Value<float>() ?? 0;
                color.b = jObject["b"]?.Value<float>() ?? 0;
                color.a = jObject["a"]?.Value<float>() ?? 0;

                return color;
            }

            private void DeserializeArrayToList(JToken? token, ref List<string>? list)
            {
                if (token is { Type: JTokenType.Array })
                {
                    list ??= new List<string>();
                    list.Clear();

                    foreach (JToken? item in token.Children())
                    {
                        string? s = item.ToObject<string>();

                        if (s != null)
                            list.Add(s);
                    }
                }
                else
                    list?.Clear();
            }
        }
    }
}
