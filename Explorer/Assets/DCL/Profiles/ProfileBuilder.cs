using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Profiles
{
    public class ProfileBuilder
    {
        private IEnumerable<URN>? wearables;
        private BodyShape bodyShape = BodyShape.MALE;
        private Color eyesColor;
        private Color hairColor;
        private Color skinColor;
        private IReadOnlyCollection<URN>? emotes;
        private HashSet<string>? forceRender;
        private HashSet<string>? blocked;
        private List<string>? interests;
        private List<string>? links;
        private DateTime? birthdate;
        private string? country;
        private string? description;
        private string? email;
        private string? gender;
        private string? hobbies;
        private string? language;
        private string? name;
        private string? profession;
        private string? pronouns;
        private int version;
        private string? employment;
        private string? realName;
        private string? relationship;
        private string? sexualOrientation;
        private int tutorialStep;
        private string? unclaimedName;
        private string? userId;
        private bool hasClaimedName;
        private bool hasConnectedWeb3;
        private URLAddress? bodySnapshotUrl;
        private URLAddress? faceSnapshotUrl;

        public ProfileBuilder From(Profile? profile)
        {
            if (profile == null)
                return From(Build());

            wearables = profile.Avatar.wearables;
            bodyShape = profile.Avatar.BodyShape;
            eyesColor = profile.Avatar.EyesColor;
            skinColor = profile.Avatar.SkinColor;
            hairColor = profile.Avatar.HairColor;
            emotes = profile.Avatar.emotes;
            forceRender = profile.Avatar.forceRender;
            bodySnapshotUrl = profile.Avatar.BodySnapshotUrl;
            faceSnapshotUrl = profile.Avatar.FaceSnapshotUrl;
            blocked = profile.blocked?.ToHashSet();
            interests = profile.interests?.ToList();
            links = profile.links?.ToList();
            birthdate = profile.Birthdate;
            country = profile.Country;
            description = profile.Description;
            email = profile.Email;
            gender = profile.Gender;
            hobbies = profile.Hobbies;
            language = profile.Language;
            name = profile.Name;
            profession = profile.Profession;
            pronouns = profile.Pronouns;
            version = profile.Version;
            employment = profile.EmploymentStatus;
            realName = profile.RealName;
            relationship = profile.RelationshipStatus;
            sexualOrientation = profile.SexualOrientation;
            tutorialStep = profile.TutorialStep;
            unclaimedName = profile.UnclaimedName;
            userId = profile.UserId;
            hasClaimedName = profile.HasClaimedName;
            hasConnectedWeb3 = profile.HasConnectedWeb3;

            return this;
        }

        public ProfileBuilder WithWearables(IEnumerable<URN> wearables)
        {
            this.wearables = wearables;
            return this;
        }

        public ProfileBuilder WithEmotes(IReadOnlyCollection<URN> emotes)
        {
            this.emotes = emotes;
            return this;
        }

        public ProfileBuilder WithForceRender(IEnumerable<string> categories)
        {
            forceRender ??= new HashSet<string>();
            forceRender.Clear();

            foreach (string category in categories)
                forceRender.Add(category);

            return this;
        }

        public ProfileBuilder WithBodyShape(BodyShape bodyShape)
        {
            this.bodyShape = bodyShape;
            return this;
        }

        public Profile Build()
        {
            var profile = new Profile();
            profile.RealName = realName ?? "";
            profile.UserId = userId!;
            profile.Version = version;
            profile.blocked = blocked;
            profile.interests = interests;
            profile.links = links;
            profile.Birthdate = birthdate;
            profile.Country = country ?? "";
            profile.Description = description ?? "";
            profile.Email = email ?? "";
            profile.Gender = gender ?? "";
            profile.Hobbies = hobbies ?? "";
            profile.Language = language ?? "";
            profile.Name = name ?? "";
            profile.Profession = profession ?? "";
            profile.Pronouns = pronouns ?? "";
            profile.Version = version;
            profile.EmploymentStatus = employment ?? "";
            profile.RelationshipStatus = relationship ?? "";
            profile.SexualOrientation = sexualOrientation ?? "";
            profile.TutorialStep = tutorialStep;
            profile.UnclaimedName = unclaimedName ?? "";
            profile.HasClaimedName = hasClaimedName;
            profile.HasConnectedWeb3 = hasConnectedWeb3;

            var avatar = new Avatar();
            profile.Avatar = avatar;

            if (wearables != null)
                foreach (URN urn in wearables)
                    avatar.wearables.Add(urn);

            avatar.BodyShape = bodyShape;

            if (emotes != null)
            {
                var i = 0;

                foreach (URN urn in emotes)
                    avatar.emotes[i++] = urn;
            }

            if (forceRender != null)
                foreach (string s in forceRender)
                    avatar.forceRender.Add(s);

            avatar.HairColor = hairColor;
            avatar.SkinColor = skinColor;
            avatar.EyesColor = eyesColor;
            avatar.BodySnapshotUrl = bodySnapshotUrl ?? URLAddress.EMPTY;
            avatar.FaceSnapshotUrl = faceSnapshotUrl ?? URLAddress.EMPTY;

            return profile;
        }
    }
}
