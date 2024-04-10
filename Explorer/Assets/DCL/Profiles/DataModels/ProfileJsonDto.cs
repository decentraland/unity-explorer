#nullable enable

using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    [Serializable]
    public struct EmoteJsonDto
    {
        public int slot;
        public string urn;
    }

    [Serializable]
    public struct AvatarColorJsonDto
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color ToColor() =>
            new (r, g, b, a);

        public void Reset()
        {
            r = 1;
            g = 1;
            b = 1;
            a = 1;
        }

        public void CopyFrom(Color color)
        {
            r = color.r;
            g = color.g;
            b = color.b;
            a = color.a;
        }
    }

    [Serializable]
    public struct EyesJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public struct HairJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public struct SkinJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public struct AvatarSnapshotJsonDto
    {
        public string face256;
        public string body;
    }

    [Serializable]
    public struct AvatarJsonDto
    {
        private static readonly ThreadSafeListPool<URN> wearableUrnPool = new (10, 10);
        private static readonly ThreadSafeListPool<string> forceRenderPool = new (15, 15);

        public string bodyShape;
        public List<string> wearables;
        public List<string> forceRender;
        public List<EmoteJsonDto> emotes;
        public AvatarSnapshotJsonDto snapshots;
        public EyesJsonDto eyes;
        public HairJsonDto hair;
        public SkinJsonDto skin;

        public void CopyTo(Avatar avatar)
        {
            List<URN> wearableUrns = wearableUrnPool.Get();
            List<string> forceRenderCategories = forceRenderPool.Get();

            foreach (string w in wearables)
                wearableUrns.Add(w);

            avatar.forceRender.Clear();

            if (forceRender != null)
                foreach (string forceRenderCategory in forceRender)
                    forceRenderCategories.Add(forceRenderCategory);

            foreach (string forceRenderCategory in forceRenderCategories)
                avatar.forceRender.Add(forceRenderCategory);

            // The wearables urns retrieved in the profile follows https://adr.decentraland.org/adr/ADR-244
            avatar.wearables.Clear();
            avatar.wearables.UnionWith(wearableUrns!);

            avatar.BodyShape = BodyShape.FromStringSafe(bodyShape);

            foreach (EmoteJsonDto emoteJsonDto in emotes)
                avatar.emotes[emoteJsonDto.slot] = emoteJsonDto.urn;

            avatar.FaceSnapshotUrl = URLAddress.FromString(snapshots!.face256);
            avatar.BodySnapshotUrl = URLAddress.FromString(snapshots.body);
            avatar.EyesColor = eyes!.color!.ToColor();
            avatar.HairColor = hair!.color!.ToColor();
            avatar.SkinColor = skin!.color!.ToColor();

            forceRenderPool.Release(forceRenderCategories);
            wearableUrnPool.Release(wearableUrns);
        }

        public void CopyFrom(Avatar avatar)
        {
            wearables ??= new List<string>();
            wearables.Clear();

            foreach (string w in avatar.wearables)
                wearables.Add(w);

            bodyShape = BodyShape.FromStringSafe(avatar.BodyShape);

            forceRender ??= new List<string>();
            forceRender.Clear();
            forceRender.AddRange(avatar.forceRender);

            emotes ??= new List<EmoteJsonDto>();
            emotes.Clear();

            for (var i = 0; i < avatar.emotes.Length; i++)
            {
                URN urn = avatar.emotes[i];
                if (urn.IsNullOrEmpty()) continue;

                var emoteDto = new EmoteJsonDto
                {
                    slot = i,
                    urn = urn,
                };
                emotes.Add(emoteDto);
            }

            // We GET the profile with full snapshot url information but the profile is saved just with the cid :/
            int faceUrlPathIndex = avatar.FaceSnapshotUrl.Value.LastIndexOf('/');
            snapshots.face256 = faceUrlPathIndex != -1 ? avatar.FaceSnapshotUrl.Value[(faceUrlPathIndex + 1)..] : avatar.FaceSnapshotUrl;
            int bodyUrlPathIndex = avatar.BodySnapshotUrl.Value.LastIndexOf('/');
            snapshots.body = bodyUrlPathIndex != -1 ? avatar.BodySnapshotUrl.Value[(bodyUrlPathIndex + 1)..] : avatar.BodySnapshotUrl;

            eyes.color.CopyFrom(avatar.EyesColor);
            hair.color.CopyFrom(avatar.HairColor);
            skin.color.CopyFrom(avatar.SkinColor);
        }

        public void Reset()
        {
            bodyShape = default(string);
            wearables?.Clear();
            forceRender?.Clear();
            emotes?.Clear();
            snapshots.face256 = default(string);
            snapshots.body = default(string);
            eyes.color.Reset();
            hair.color.Reset();
            skin.color.Reset();
        }
    }

    [Serializable]
    public class ProfileJsonDto : IDisposable
    {
        private static readonly ThreadSafeObjectPool<ProfileJsonDto> POOL = new (() => new ProfileJsonDto());

        public bool hasClaimedName;
        public string description;
        public int tutorialStep;
        public string name;
        public string userId;
        public string email;
        public string ethAddress;
        public int version;
        public AvatarJsonDto avatar;
        public List<string> blocked;
        public List<string> interests;
        public string unclaimedName;
        public bool hasConnectedWeb3;
        public string country;
        public string employmentStatus;
        public string gender;
        public string pronouns;
        public string relationshipStatus;
        public string sexualOrientation;
        public string language;
        public string profession;
        public string realName;
        public string hobbies;
        public long birthdate;
        public List<string> links;

        public void Dispose()
        {
            POOL.Release(this);
        }

        public static ProfileJsonDto Create()
        {
            ProfileJsonDto profile = POOL.Get();
            profile.Reset();
            return profile;
        }

        public void CopyTo(Profile profile)
        {
            profile.UserId = userId;
            profile.Name = name;
            profile.UnclaimedName = unclaimedName;
            profile.HasClaimedName = hasClaimedName;
            profile.HasConnectedWeb3 = hasConnectedWeb3;
            profile.Description = description;
            profile.TutorialStep = tutorialStep;
            profile.Email = email;
            profile.Version = version;
            profile.Country = country;
            profile.EmploymentStatus = employmentStatus;
            profile.Gender = gender;
            profile.Pronouns = pronouns;
            profile.RelationshipStatus = relationshipStatus;
            profile.SexualOrientation = sexualOrientation;
            profile.Language = language;
            profile.Profession = profession;
            profile.RealName = realName;
            profile.Birthdate = birthdate != 0 ? DateTimeOffset.FromUnixTimeSeconds(birthdate).DateTime : null;
            profile.Hobbies = hobbies;
            profile.Avatar ??= new Avatar();
            avatar.CopyTo(profile.Avatar);

            if (blocked != null)
            {
                profile.blocked ??= HashSetPool<string>.Get();
                profile.blocked.Clear();
                profile.blocked.UnionWith(blocked);
            }
            else if (profile.blocked != null)
            {
                HashSetPool<string>.Release(profile.blocked);
                profile.blocked = null;
            }

            if (interests != null)
            {
                profile.interests ??= ListPool<string>.Get();
                profile.interests.Clear();
                profile.interests.AddRange(interests);
            }
            else if (profile.interests != null)
            {
                ListPool<string>.Release(profile.interests);
                profile.interests = null;
            }

            if (links != null)
            {
                profile.links ??= ListPool<string>.Get();
                profile.links.Clear();
                profile.links.AddRange(links);
            }
            else if (profile.links != null)
            {
                ListPool<string>.Release(profile.links);
                profile.links = null;
            }
        }

        public void CopyFrom(Profile profile)
        {
            userId = profile.UserId;
            ethAddress = profile.UserId;
            name = profile.Name;
            unclaimedName = profile.UnclaimedName;
            hasClaimedName = profile.HasClaimedName;
            description = profile.Description;
            tutorialStep = profile.TutorialStep;
            email = profile.Email;
            version = profile.Version;
            avatar.CopyFrom(profile.Avatar);
            hasConnectedWeb3 = profile.HasConnectedWeb3;
            country = profile.Country;
            employmentStatus = profile.EmploymentStatus;
            gender = profile.Gender;
            pronouns = profile.Pronouns;
            relationshipStatus = profile.RelationshipStatus;
            sexualOrientation = profile.SexualOrientation;
            language = profile.Language;
            profession = profile.Profession;
            realName = profile.RealName;
            birthdate = profile.Birthdate != null ? new DateTimeOffset(profile.Birthdate.Value).ToUnixTimeSeconds() : 0;
            hobbies = profile.Hobbies;

            if (profile.blocked != null)
            {
                blocked ??= ListPool<string>.Get();
                blocked.Clear();
                blocked.AddRange(profile.blocked);
            }
            else if (blocked != null)
            {
                ListPool<string>.Release(blocked);
                blocked = null;
            }

            if (profile.interests != null)
            {
                interests ??= ListPool<string>.Get();
                interests.Clear();
                interests.AddRange(profile.interests);
            }
            else if (interests != null)
            {
                ListPool<string>.Release(interests);
                interests = null;
            }

            if (profile.links != null)
            {
                links ??= ListPool<string>.Get();
                links.Clear();
                links.AddRange(profile.links);
            }
            else if (links != null)
            {
                ListPool<string>.Release(links);
                links = null;
            }
        }

        private void Reset()
        {
            hasClaimedName = default(bool);
            description = default(string);
            tutorialStep = default(int);
            name = default(string);
            userId = default(string);
            email = default(string);
            ethAddress = default(string);
            version = default(int);
            avatar.Reset();
            blocked?.Clear();
            interests?.Clear();
            unclaimedName = default(string);
            hasConnectedWeb3 = default(bool);
            country = default(string);
            employmentStatus = default(string);
            gender = default(string);
            pronouns = default(string);
            relationshipStatus = default(string);
            sexualOrientation = default(string);
            language = default(string);
            profession = default(string);
            realName = default(string);
            birthdate = default(long);
            hobbies = default(string);
            links?.Clear();
        }
    }

    [Serializable]
    public class GetProfileJsonRootDto : IDisposable
    {
        private static readonly ThreadSafeObjectPool<GetProfileJsonRootDto> POOL = new (() => new GetProfileJsonRootDto());

        public List<ProfileJsonDto> avatars;

        private GetProfileJsonRootDto() { }

        public void Dispose()
        {
            if (avatars != null)
                foreach (ProfileJsonDto avatar in avatars)
                    avatar.Dispose();

            POOL.Release(this);
        }

        public static GetProfileJsonRootDto Create()
        {
            GetProfileJsonRootDto root = POOL.Get();
            root.avatars?.Clear();
            return root;
        }

        public void CopyFrom(Profile profile)
        {
            avatars ??= new List<ProfileJsonDto>();
            avatars.Clear();
            var profileDto = new ProfileJsonDto();
            profileDto.CopyFrom(profile);
            avatars.Add(profileDto);
        }

        private bool AnyAvatarInList()
        {
            if (avatars == null) return false;
            if (avatars.Count == 0) return false;

            return true;
        }

        public ProfileJsonDto? FirstProfileDto() =>
            AnyAvatarInList() ? avatars[0] : null;
    }
}
