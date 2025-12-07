using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Scripting;

namespace DCL.Profiles
{
    /// <summary>
    ///     Converts JSON DTO directly to the <see cref="Profile" /> without a need of an intermediate DTO object
    /// </summary>
    [Preserve]
    public class ProfileConverter : JsonConverter<Profile>
    {
        public readonly struct SerializationContext
        {
            public readonly string FaceHash;
            public readonly string BodyHash;

            public SerializationContext(string faceHash, string bodyHash)
            {
                FaceHash = faceHash;
                BodyHash = bodyHash;
            }
        }

        public override void WriteJson(JsonWriter writer, Profile? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("avatars");
            writer.WriteStartArray();
            SerializeProfile(writer, (SerializationContext)serializer.Context.Context, value);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public override Profile? ReadJson(JsonReader reader, Type objectType, Profile? existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jObject = JObject.Load(reader);
            existingValue ??= Profile.Create();
            DeserializeProfileList(jObject, existingValue);
            return existingValue;
        }

        private void DeserializeProfileList(JToken root, Profile profile)
        {
            // Temporary support two schemes: from catalyst and centralized
            // TODO remove before releasing

            JToken? metadata = root["metadata"];

            if (metadata != null)
                root = metadata;

            JToken? avatars = root["avatars"];

            if (avatars is { Type: JTokenType.Array })
            {
                foreach (JToken? item in avatars.Children())
                {
                    DeserializeProfile(item, profile);

                    // We only care about the first element, it's never an array in reality
                    break;
                }
            }
            else
                throw new ArgumentException("\"avatars\" is not a JSON array");


        }

        private void DeserializeProfile(JToken? jObject, Profile profile)
        {
            if (jObject == null) return;

            profile.GetCompact() = ProfileCompactInfoConverter.ReadJson(jObject);

            profile.Description = jObject["description"]?.Value<string>() ?? "";
            profile.TutorialStep = jObject["tutorialStep"]?.Value<int>() ?? 0;
            profile.Email = jObject["email"]?.Value<string>() ?? "";

            // profile.ethAddress = jObject["ethAddress"]?.Value<string>() ?? ""; NOT USED
            profile.Version = jObject["version"]?.Value<int>() ?? 0;
            profile.HasConnectedWeb3 = jObject["hasConnectedWeb3"]?.Value<bool>() ?? false;

            DeserializeAvatar(jObject["avatar"]!, ref profile);
            profile.GetCompact().FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl;

            profile.Country = jObject["country"]?.Value<string>() ?? "";
            profile.Gender = jObject["gender"]?.Value<string>() ?? "";
            profile.Pronouns = jObject["pronouns"]?.Value<string>() ?? "";
            profile.RelationshipStatus = jObject["relationshipStatus"]?.Value<string>() ?? "";
            profile.SexualOrientation = jObject["sexualOrientation"]?.Value<string>() ?? "";
            profile.Language = jObject["language"]?.Value<string>() ?? "";
            profile.EmploymentStatus = jObject["employmentStatus"]?.Value<string>() ?? "";
            profile.Profession = jObject["profession"]?.Value<string>() ?? "";
            profile.RealName = jObject["realName"]?.Value<string>() ?? "";
            profile.Hobbies = jObject["hobbies"]?.Value<string>() ?? "";

            long birthdate = jObject["birthDate"]?.Value<long>() ?? 0;
            profile.Birthdate = birthdate != 0 ? DateTimeOffset.FromUnixTimeSeconds(birthdate).DateTime : null;

            DeserializeLinks(jObject["links"]!, ref profile.links);
            DeserializeArrayToCollection(jObject["blocked"], ref profile.blocked, static s => s);
            DeserializeArrayToCollection(jObject["interests"], ref profile.interests, static s => s);
        }

        private void DeserializeLinks(JToken? root, ref List<LinkJsonDto>? list)
        {
            list?.Clear();

            if (root is { Type: JTokenType.Array })
            {
                list ??= ListPool<LinkJsonDto>.Get();

                foreach (JToken? item in root.Children())
                    list.Add(DeserializeLink(item, new LinkJsonDto()));
            }
        }

        private LinkJsonDto DeserializeLink(JToken item, LinkJsonDto link)
        {
            link.title = item["title"]?.Value<string>() ?? "";
            link.url = item["url"]?.Value<string>() ?? "";
            return link;
        }

        private void DeserializeAvatar(JToken jObject, ref Profile profile)
        {
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            Avatar avatar = profile.Avatar ??= new Avatar();

            avatar.EyesColor = DeserializeColor(jObject["eyes"]?["color"], Color.black);
            avatar.HairColor = DeserializeColor(jObject["hair"]?["color"], Color.black);
            avatar.SkinColor = DeserializeColor(jObject["skin"]?["color"], Color.black);

            avatar.BodyShape = BodyShape.FromStringSafe(jObject["bodyShape"]?.Value<string>() ?? "");

            DeserializeArrayToHashSet(jObject["wearables"], avatar.wearables, static s => new URN(s));
            DeserializeArrayToHashSet(jObject["forceRender"], avatar.forceRender, static s => s);

            DeserializeEmoteList(jObject["emotes"], avatar.emotes);

            avatar.FaceSnapshotUrl = URLAddress.FromString(jObject["snapshots"]?["face256"]?.Value<string>() ?? "");
            avatar.BodySnapshotUrl = URLAddress.FromString(jObject["snapshots"]?["body"]?.Value<string>() ?? "");
        }

        private void DeserializeEmoteList(JToken? root, URN[] equippedEmotes)
        {
            Array.Clear(equippedEmotes, 0, equippedEmotes.Length);

            if (root is { Type: JTokenType.Array })
            {
                foreach (JToken? item in root.Children())
                {
                    int slot = item["slot"]?.Value<int>() ?? 0;
                    string urn = item["urn"]?.Value<string>() ?? "";

                    equippedEmotes[slot] = urn;
                }
            }
        }

        private Color DeserializeColor(JToken? jObject, Color color)
        {
            if (jObject == null) return color;

            color.r = jObject["r"]?.Value<float>() ?? 0;
            color.g = jObject["g"]?.Value<float>() ?? 0;
            color.b = jObject["b"]?.Value<float>() ?? 0;
            color.a = jObject["a"]?.Value<float>() ?? 1;

            return color;
        }

        private void DeserializeArrayToHashSet<TResult>(JToken? token, HashSet<TResult> set, Func<string, TResult> selector)
        {
            set.Clear();

            if (token is { Type: JTokenType.Array })
            {
                foreach (JToken? item in token.Children())
                {
                    string? s = item.ToObject<string>();

                    if (s != null)
                        set.Add(selector(s));
                }
            }
        }

        private void DeserializeArrayToCollection<TCollection, T>(JToken? token, ref TCollection? list, Func<string, T> selector)
            where TCollection: class, ICollection<T>, new()
        {
            list?.Clear();

            if (token is { Type: JTokenType.Array })
            {
                list ??= CollectionPool<TCollection, T>.Get();

                foreach (JToken? item in token.Children())
                {
                    string? s = item.ToObject<string>();

                    if (s != null)
                        list.Add(selector(s));
                }
            }
        }

        private void SerializeProfile(JsonWriter writer, SerializationContext context, Profile profile)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("hasClaimedName");
            writer.WriteValue(profile.HasClaimedName);

            writer.WritePropertyName("description");
            writer.WriteValue(profile.Description);

            writer.WritePropertyName("tutorialStep");
            writer.WriteValue(profile.TutorialStep);

            writer.WritePropertyName("name");
            writer.WriteValue(profile.Name);

            writer.WritePropertyName("userId");
            writer.WriteValue(profile.UserId);

            writer.WritePropertyName("email");
            writer.WriteValue(profile.Email);

            writer.WritePropertyName("ethAddress");
            writer.WriteValue(profile.UserId);

            writer.WritePropertyName("version");
            writer.WriteValue(profile.Version);

            writer.WritePropertyName("unclaimedName");
            writer.WriteValue(profile.UnclaimedName);

            writer.WritePropertyName("hasConnectedWeb3");
            writer.WriteValue(profile.HasConnectedWeb3);

            writer.WritePropertyName("avatar");
            SerializeAvatar(writer, context, profile);

            writer.WritePropertyName("country");
            writer.WriteValue(profile.Country);

            writer.WritePropertyName("gender");
            writer.WriteValue(profile.Gender);

            writer.WritePropertyName("pronouns");
            writer.WriteValue(profile.Pronouns);

            writer.WritePropertyName("relationshipStatus");
            writer.WriteValue(profile.RelationshipStatus);

            writer.WritePropertyName("sexualOrientation");
            writer.WriteValue(profile.SexualOrientation);

            writer.WritePropertyName("language");
            writer.WriteValue(profile.Language);

            writer.WritePropertyName("employmentStatus");
            writer.WriteValue(profile.EmploymentStatus);

            writer.WritePropertyName("profession");
            writer.WriteValue(profile.Profession);

            writer.WritePropertyName("realName");
            writer.WriteValue(profile.RealName);

            writer.WritePropertyName("hobbies");
            writer.WriteValue(profile.Hobbies);

            writer.WritePropertyName("birthDate");
            writer.WriteValue(profile.Birthdate != null ? new DateTimeOffset(profile.Birthdate.Value).ToUnixTimeSeconds() : 0);

            writer.WritePropertyName("links");
            SerializeLinks(writer, profile.links);

            writer.WritePropertyName("blocked");
            SerializeCollectionToArray(writer, profile.blocked);

            writer.WritePropertyName("interests");
            SerializeCollectionToArray(writer, profile.interests);

            writer.WriteEndObject();
        }

        private void SerializeAvatar(JsonWriter writer, SerializationContext context, Profile profile)
        {
            Avatar avatar = profile.Avatar;

            writer.WriteStartObject();

            writer.WritePropertyName("bodyShape");
            writer.WriteValue(avatar.BodyShape.Value);

            writer.WritePropertyName("wearables");
            SerializeHashSetToArray(writer, avatar.wearables, static urn => urn.ToString());

            writer.WritePropertyName("forceRender");
            SerializeHashSetToArray(writer, avatar.forceRender, static s => s);

            writer.WritePropertyName("emotes");
            SerializeEmoteList(writer, avatar.emotes);

            writer.WritePropertyName("snapshots");
            writer.WriteStartObject();

            writer.WritePropertyName("face256");
            writer.WriteValue(context.FaceHash);
            writer.WritePropertyName("body");
            writer.WriteValue(context.BodyHash);
            writer.WriteEndObject();

            writer.WritePropertyName("eyes");
            writer.WriteStartObject();
            writer.WritePropertyName("color");
            SerializeColor(writer, avatar.EyesColor);
            writer.WriteEndObject();

            writer.WritePropertyName("hair");
            writer.WriteStartObject();
            writer.WritePropertyName("color");
            SerializeColor(writer, avatar.HairColor);
            writer.WriteEndObject();

            writer.WritePropertyName("skin");
            writer.WriteStartObject();
            writer.WritePropertyName("color");
            SerializeColor(writer, avatar.SkinColor);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        private void SerializeColor(JsonWriter writer, Color color)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(color.r);
            writer.WritePropertyName("g");
            writer.WriteValue(color.g);
            writer.WritePropertyName("b");
            writer.WriteValue(color.b);
            writer.WritePropertyName("a");
            writer.WriteValue(color.a);
            writer.WriteEndObject();
        }

        private void SerializeLinks(JsonWriter writer, List<LinkJsonDto>? links)
        {
            writer.WriteStartArray();

            if (links != null)
            {
                foreach (LinkJsonDto link in links)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("title");
                    writer.WriteValue(link.title);
                    writer.WritePropertyName("url");
                    writer.WriteValue(link.url);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
        }

        private void SerializeEmoteList(JsonWriter writer, URN[] emotes)
        {
            writer.WriteStartArray();

            for (int i = 0; i < emotes.Length; i++)
            {
                URN urn = emotes[i];
                if (urn.IsNullOrEmpty()) continue;

                writer.WriteStartObject();
                writer.WritePropertyName("slot");
                writer.WriteValue(i);
                writer.WritePropertyName("urn");
                writer.WriteValue(urn.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private void SerializeHashSetToArray<T>(JsonWriter writer, HashSet<T> set, Func<T, string> selector)
        {
            writer.WriteStartArray();

            foreach (T item in set)
                writer.WriteValue(selector(item));

            writer.WriteEndArray();
        }

        private void SerializeCollectionToArray<T>(JsonWriter writer, ICollection<T>? collection)
        {
            writer.WriteStartArray();

            if (collection != null)
            {
                foreach (T item in collection)
                    writer.WriteValue(item);
            }

            writer.WriteEndArray();
        }
    }
}
