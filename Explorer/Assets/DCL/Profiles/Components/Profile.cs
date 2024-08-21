using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.Optimization.ThreadSafePool;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Profiles
{
    public class Profile : IDirtyMarker, IDisposable
    {
        private static readonly Regex VALID_NAME_CHARACTERS = new ("[a-zA-Z0-9]");
        private static readonly ThreadSafeObjectPool<Profile> POOL = new (
            () => new Profile(),
            actionOnRelease: profile => profile.Clear());

        internal HashSet<string>? blocked;
        internal List<string>? interests;
        internal List<LinkJsonDto>? links;

        private string userId;
        private string name;
        private bool hasClaimedName;
        public StreamableLoadingResult<Sprite>? ProfilePicture { get; set; }

        public string UserId
        {
            get => userId;

            internal set
            {
                userId = value;
                DisplayName = GenerateDisplayName();
            }
        }

        public string Name
        {
            get => name;

            internal set
            {
                name = value;
                DisplayName = GenerateDisplayName();
            }
        }

        public string DisplayName { get; private set; }
        public string UnclaimedName { get; internal set; }

        public bool HasClaimedName
        {
            get => hasClaimedName;

            internal set
            {
                hasClaimedName = value;
                DisplayName = GenerateDisplayName();
            }
        }

        public bool HasConnectedWeb3 { get; internal set; }
        public string? Description { get; set; }
        public int TutorialStep { get; internal set; }
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
        public int Version { get; internal set; }
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

        public static Profile Create() =>
            POOL.Get();

        public static Profile Create(string userId, string name, Avatar avatar)
        {
            Profile profile = Create();
            profile.UserId = userId;
            profile.Name = name;
            profile.Avatar = avatar;
            return profile;
        }

        internal Profile() { }

        internal Profile(string userId, string name, Avatar avatar)
        {
            UserId = userId;
            Name = name;
            Avatar = avatar;
        }

        public void Clear()
        {
            this.blocked?.Clear();
            this.interests?.Clear();
            this.links?.Clear();
            this.Birthdate = null;
            this.Avatar.Clear();
            this.Country = default(string?);
            this.Email = default(string?);
            this.Gender = default(string?);
            this.Description = default(string?);
            this.Hobbies = default(string?);
            this.Language = default(string?);
            this.Profession = default(string?);
            this.Pronouns = default(string?);
            this.Version = default(int);
            this.HasClaimedName = default(bool);
            this.EmploymentStatus = default(string?);
            this.UserId = "";
            this.Name = "";
            this.TutorialStep = default(int);
            this.HasConnectedWeb3 = default(bool);
            this.IsDirty = false;
        }

        public static Profile NewRandomProfile(string? userId) =>
            new (
                userId ?? IProfileRepository.GUEST_RANDOM_ID,
                IProfileRepository.PLAYER_RANDOM_ID,
                new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor()
                )
            );

        public void Dispose() =>
            POOL.Release(this);

        private string GenerateDisplayName()
        {
            if (string.IsNullOrEmpty(Name)) return "";

            var result = "";
            MatchCollection matches = VALID_NAME_CHARACTERS.Matches(Name);

            foreach (Match match in matches)
                result += match.Value;

            if (HasClaimedName)
                return result;

            return string.IsNullOrEmpty(UserId) || UserId.Length < 4 ? result : $"{result}#{UserId[^4..]}";
        }

        public void ClearLinks()
        {
            if (Links == null)
                Links = new List<LinkJsonDto>();
            else
                Links.Clear();
        }
    }
}
