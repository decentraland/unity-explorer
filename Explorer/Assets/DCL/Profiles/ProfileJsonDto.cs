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

        public Eyes ToEyes() =>
            new ()
            {
                Color = color.ToColor(),
            };
    }

    [Serializable]
    public class HairJsonDto
    {
        public AvatarColorJsonDto color;

        public Hair ToHair() =>
            new ()
            {
                Color = color.ToColor(),
            };
    }

    [Serializable]
    public class SkinJsonDto
    {
        public AvatarColorJsonDto color;

        public Skin ToSkin() =>
            new ()
            {
                Color = color.ToColor(),
            };
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
            return new Avatar
            {
                Wearables = new HashSet<string>(wearables),
                Emotes = emotes.ToDictionary(dto => dto.urn, dto => dto.ToEmote()),
                Eyes = eyes.ToEyes(),
                Hair = hair.ToHair(),
                Skin = skin.ToSkin(),
                FaceSnapshotUrl = URLAddress.FromString(snapshots.face256),
                BodySnapshotUrl = URLAddress.FromString(snapshots.body),
                BodyShape = bodyShape,
                ForceRender = new HashSet<string>(forceRender),
            };
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
            new ()
            {
                UnclaimedName = unclaimedName,
                Avatar = avatar.ToAvatar(),
                Blocked = new HashSet<string>(blocked),
                Description = description,
                Email = email,
                Interests = new List<string>(interests),
                Name = name,
                Version = version,
                EthAddress = ethAddress,
                TutorialStep = tutorialStep,
                UserId = userId,
                HasClaimedName = hasClaimedName,
            };
    }
}
