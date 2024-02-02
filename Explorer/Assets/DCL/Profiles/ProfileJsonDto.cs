#nullable disable

using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Profiles
{
    [Serializable]
    public struct EmoteJsonDto
    {
        public int slot;
        public string urn;

        public Emote ToEmote() =>
            new (slot, urn);

        public void CopyFrom(Emote emote)
        {
            slot = emote.Slot;
            urn = emote.Urn;
        }
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
            const int SHARED_WEARABLES_MAX_URN_PARTS = 6;

            List<URN> wearableUrns = wearableUrnPool.Get();

            foreach (string w in wearables)
                wearableUrns.Add(w);

            avatar.sharedWearables.Clear();

            foreach (URN wearable in wearableUrns)
                avatar.sharedWearables.Add(wearable.Shorten(SHARED_WEARABLES_MAX_URN_PARTS));

            // The wearables urns retrieved in the profile follows https://adr.decentraland.org/adr/ADR-244
            avatar.uniqueWearables.Clear();
            avatar.uniqueWearables.UnionWith(wearableUrns!);

            avatar.BodyShape = BodyShape.FromStringSafe(bodyShape);
            emotes.AlignWithDictionary(avatar.emotes, static dto => dto.urn, static dto => dto.ToEmote());

            avatar.FaceSnapshotUrl = URLAddress.FromString(snapshots!.face256);
            avatar.BodySnapshotUrl = URLAddress.FromString(snapshots.body);
            avatar.EyesColor = eyes!.color!.ToColor();
            avatar.HairColor = hair!.color!.ToColor();
            avatar.SkinColor = skin!.color!.ToColor();

            wearableUrnPool.Release(wearableUrns);
        }

        public void CopyFrom(Avatar avatar)
        {
            wearables ??= new List<string>();
            wearables.Clear();

            foreach (string w in avatar.uniqueWearables)
                wearables.Add(w);

            bodyShape = BodyShape.FromStringSafe(avatar.BodyShape);

            emotes ??= new List<EmoteJsonDto>();
            emotes.Clear();

            foreach ((string urn, Emote emote) in avatar.emotes)
            {
                var emoteDto = new EmoteJsonDto();
                emoteDto.CopyFrom(emote);
                emotes.Add(emoteDto);
            }

            snapshots.face256 = avatar.FaceSnapshotUrl;
            snapshots.body = avatar.BodySnapshotUrl;

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

        public static ProfileJsonDto Create()
        {
            ProfileJsonDto profile = POOL.Get();
            profile.Reset();
            return profile;
        }

        public void Dispose()
        {
            POOL.Release(this);
        }

        public void CopyTo(Profile profile)
        {
            profile.UserId = userId;
            profile.Name = name;
            profile.UnclaimedName = unclaimedName;
            profile.HasClaimedName = hasClaimedName;
            profile.Description = description;
            profile.TutorialStep = tutorialStep;
            profile.Email = email;
            profile.Version = version;
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
        }

        public void CopyFrom(Profile profile)
        {
            userId = profile.UserId;
            name = profile.Name;
            unclaimedName = profile.UnclaimedName;
            hasClaimedName = profile.HasClaimedName;
            description = profile.Description;
            tutorialStep = profile.TutorialStep;
            email = profile.Email;
            version = profile.Version;
            avatar.CopyFrom(profile.Avatar);

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
        }
    }

    [Serializable]
    public class GetProfileJsonRootDto : IDisposable
    {
        private static readonly ThreadSafeObjectPool<GetProfileJsonRootDto> POOL = new (() => new GetProfileJsonRootDto());

        public List<ProfileJsonDto> avatars;

        public static GetProfileJsonRootDto Create()
        {
            GetProfileJsonRootDto root = POOL.Get();
            root.avatars?.Clear();
            return root;
        }

        private GetProfileJsonRootDto() { }

        public void Dispose()
        {
            if (avatars != null)
                foreach (ProfileJsonDto avatar in avatars)
                    avatar.Dispose();

            POOL.Release(this);
        }

        public void CopyFrom(Profile profile)
        {
            avatars ??= new List<ProfileJsonDto>();
            avatars.Clear();
            var profileDto = new ProfileJsonDto();
            profileDto.CopyFrom(profile);
            avatars.Add(profileDto);
        }
    }
}
