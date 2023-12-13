using CommunicationData.URLHelpers;
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
            new ()
            {
                Slot = slot,
                Urn = urn,
            };
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

        public Avatar ToAvatar()
        {
            const int SHARED_WEARABLES_MAX_URN_PARTS = 6;

            var sharedWearables = new HashSet<string>(wearables.Length);

            foreach (string wearable in wearables)
                sharedWearables.Add(wearable.ShortenURN(SHARED_WEARABLES_MAX_URN_PARTS));

            return new Avatar(bodyShape,

                // To avoid inconsistencies in the wearable references thus improving cache miss rate,
                // we keep a list of shared wearables used by avatar shapes and most of the rendering systems
                sharedWearables,
                // The wearables urns retrieved in the profile follows https://adr.decentraland.org/adr/ADR-244
                new HashSet<string>(wearables),
                new HashSet<string>(forceRender),
                emotes.ToDictionary(dto => dto.urn, dto => dto.ToEmote()),
                URLAddress.FromString(snapshots.face256), URLAddress.FromString(snapshots.body),
                eyes.color.ToColor(), hair.color.ToColor(), skin.color.ToColor());
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

        public Profile ToProfile() =>
            new (userId, name, unclaimedName, hasClaimedName, description, tutorialStep,
                email, version, avatar.ToAvatar(), blocked != null ? new HashSet<string>(blocked) : new HashSet<string>(),
                interests != null ? new List<string>(interests) : new List<string>());
    }
}
