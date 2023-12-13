using CommunicationData.URLHelpers;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Profiles
{
    [Serializable]
    public class EmoteJsonDto
    {
        public int slot;
        public string urn;

        public Emote ToEmote() =>
            new (slot, urn);
    }

    [Serializable]
    public class AvatarColorJsonDto
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color ToColor() =>
            new (r, g, b, a);
    }

    [Serializable]
    public class EyesJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public class HairJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public class SkinJsonDto
    {
        public AvatarColorJsonDto color;
    }

    [Serializable]
    public class AvatarSnapshotJsonDto
    {
        public string face256;
        public string body;
    }

    [Serializable]
    public class AvatarJsonDto
    {
        public string bodyShape;
        public string[] wearables;
        public string[] forceRender;
        public List<EmoteJsonDto> emotes;
        public AvatarSnapshotJsonDto snapshots;
        public EyesJsonDto eyes;
        public HairJsonDto hair;
        public SkinJsonDto skin;

        public void Reset()
        {
            bodyShape = default(string);
            wearables = default(string[]);
            forceRender = default(string[]);
            emotes = default(List<EmoteJsonDto>);
            snapshots = default(AvatarSnapshotJsonDto);
            eyes = default(EyesJsonDto);
            hair = default(HairJsonDto);
            skin = default(SkinJsonDto);
        }

        public void CopyTo(Avatar avatar)
        {
            const int SHARED_WEARABLES_MAX_URN_PARTS = 6;

            var sharedWearables = new HashSet<string>(wearables.Length);

            foreach (string wearable in wearables)
                sharedWearables.Add(wearable.ShortenURN(SHARED_WEARABLES_MAX_URN_PARTS));

            // To avoid inconsistencies in the wearable references thus improving cache miss rate,
            // we keep a list of shared wearables used by avatar shapes and most of the rendering systems
            avatar.SharedWearables = sharedWearables;
            // The wearables urns retrieved in the profile follows https://adr.decentraland.org/adr/ADR-244
            avatar.UniqueWearables = new HashSet<string>(wearables);
            avatar.BodyShape = bodyShape;
            avatar.Emotes = emotes.ToDictionary(dto => dto.urn, dto => dto.ToEmote());
            avatar.FaceSnapshotUrl = URLAddress.FromString(snapshots.face256);
            avatar.BodySnapshotUrl = URLAddress.FromString(snapshots.body);
            avatar.EyesColor = eyes.color.ToColor();
            avatar.HairColor = hair.color.ToColor();
            avatar.SkinColor = skin.color.ToColor();
        }
    }

    [Serializable]
    public class ProfileJsonDto
    {
        public bool hasClaimedName;
        public string description;
        public int tutorialStep;
        public string name;
        public string userId;
        public string email;
        public string ethAddress;
        public int version;
        public AvatarJsonDto avatar;
        public string[] blocked;
        public string[] interests;
        public string unclaimedName;
        public bool hasConnectedWeb3;

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
            avatar?.CopyTo(profile.Avatar);
            profile.Blocked = blocked != null ? new HashSet<string>(blocked) : new HashSet<string>();
            profile.Interests = interests != null ? new List<string>(interests) : new List<string>();
        }

        public void Reset()
        {
            hasClaimedName = default(bool);
            description = default(string);
            tutorialStep = default(int);
            name = default(string);
            userId = default(string);
            email = default(string);
            ethAddress = default(string);
            version = default(int);
            avatar?.Reset();
            blocked = default(string[]);
            interests = default(string[]);
            unclaimedName = default(string);
            hasConnectedWeb3 = default(bool);
        }
    }

    [Serializable]
    public class GetProfileJsonRootDto : IDisposable
    {
        private static readonly ThreadSafeObjectPool<GetProfileJsonRootDto> pool = new (() => new GetProfileJsonRootDto());

        public long timestamp;
        public ProfileJsonDto[] avatars;

        public static GetProfileJsonRootDto Create()
        {
            GetProfileJsonRootDto root = pool.Get();
            root.Reset();
            return root;
        }

        private GetProfileJsonRootDto() { }

        public void Dispose()
        {
            pool.Release(this);
        }

        private void Reset()
        {
            timestamp = default(long);

            if (avatars != null)
                foreach (ProfileJsonDto avatar in avatars)
                    avatar.Reset();
        }
    }
}
