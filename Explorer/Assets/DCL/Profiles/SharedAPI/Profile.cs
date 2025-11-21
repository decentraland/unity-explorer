using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.Optimization.ThreadSafePool;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Profiles
{
    public partial class Profile : IDirtyMarker, IDisposable
    {
        public static readonly Regex VALID_NAME_CHARACTERS = new ("[a-zA-Z0-9]");

        private static readonly ThreadSafeObjectPool<Profile> POOL = new (
            () => new Profile(),
            actionOnRelease: profile => profile.Clear());

        internal HashSet<string>? blocked;
        internal List<string>? interests;
        internal List<LinkJsonDto>? links;

        public StreamableLoadingResult<SpriteData>.WithFallback? ProfilePicture { get; set; }

        public bool HasConnectedWeb3 { get; set; }
        public string? Description { get; set; }
        public int TutorialStep { get; set; }
        public string? Email { get; internal set; }
        public string? Country { get; set; }
        public string? EmploymentStatus { get; set; }
        public string? Gender { get; set; }
        public string? Pronouns { get; set; }
        public string? RelationshipStatus { get; set; }
        public string? SexualOrientation { get; set; }
        public string? Language { get; set; }
        public string? Profession { get; set; }
        public string? RealName { get; set; }
        public string? Hobbies { get; set; }
        public DateTime? Birthdate { get; set; }
        public int Version { get; set; }
        public Avatar Avatar { get; internal set; }

        /// <summary>
        ///     This flag can be moved elsewhere when the final flow is established
        /// </summary>
        public bool IsDirty { get; set; } = true;

        public IReadOnlyCollection<string>? Blocked => blocked;
        public IReadOnlyCollection<string>? Interests => interests;

        public List<LinkJsonDto>? Links
        {
            get => links;
            set => links = value;
        }

        private CompactInfo compact;

        public CompactInfo Compact => compact;

        public Profile()
        {
            compact = new CompactInfo();
        }

        public Profile(string userId, string name, Avatar avatar)
        {
            compact = new CompactInfo(userId, name);
            Avatar = avatar;
        }

        public void Dispose()
        {
            ProfilePicture.TryDereference();
            POOL.Release(this);
        }

        public static Profile Create() =>
            POOL.Get();

        public static Profile Create(string userId, string name, Avatar avatar)
        {
            Profile profile = Create();
            profile.GetCompact().UserId = userId;
            profile.GetCompact().Name = name;
            profile.Avatar = avatar;
            return profile;
        }

        public void Clear()
        {
            Compact.Clear();
            blocked?.Clear();
            interests?.Clear();
            links?.Clear();
            Birthdate = null;
            Avatar.Clear();
            Country = null;
            Email = null;
            Gender = null;
            Description = null;
            Hobbies = null;
            Language = default(string?);
            Profession = default(string?);
            Pronouns = default(string?);
            Version = default(int);
            EmploymentStatus = default(string?);
            TutorialStep = default(int);
            HasConnectedWeb3 = default(bool);
            ProfilePicture = null;
            IsDirty = false;
        }

        public static Profile NewRandomProfile(string? userId) =>
            new (
                userId: userId ?? IProfileRepository.GUEST_RANDOM_ID,
                IProfileRepository.PLAYER_RANDOM_ID,
                new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor()
                )
            );

        public static Profile NewProfileWithAvatar(string? userId, Avatar avatar) =>
            new (
                userId: userId ?? IProfileRepository.GUEST_RANDOM_ID,
                IProfileRepository.PLAYER_RANDOM_ID,
                avatar
            );

        public void ClearLinks()
        {
            if (Links == null)
                Links = new List<LinkJsonDto>();
            else
                Links.Clear();
        }

        public bool IsSameProfile(Profile profile)
        {
            if (!Avatar.IsSameAvatar(profile.Avatar)) return false;

            return Compact.Equals(profile.Compact)
                   && HasConnectedWeb3 == profile.HasConnectedWeb3
                   && Description == profile.Description
                   && TutorialStep == profile.TutorialStep
                   && Email == profile.Email
                   && Country == profile.Country
                   && EmploymentStatus == profile.EmploymentStatus
                   && Gender == profile.Gender
                   && Pronouns == profile.Pronouns
                   && Language == profile.Language
                   && Profession == profile.Profession
                   && Birthdate == profile.Birthdate
                   && Version == profile.Version;
        }
    }
}
