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
        private string mentionName;
        private bool hasClaimedName;

        public StreamableLoadingResult<SpriteData>.WithFallback? ProfilePicture { get; set; }

        /// <summary>
        /// Is the complete wallet address of the user
        /// </summary>
        public string UserId
        {
            get => userId;
            internal set
            {
                userId = value;
                GenerateAndValidateName();
            }
        }

        public string Name
        {
            get => name;
            set
            {
                name = value;
                GenerateAndValidateName();
            }
        }

        /// <summary>
        ///     The color calculated for this username
        /// </summary>
        public Color UserNameColor { get; internal set; } = Color.white;

        /// <summary>
        ///     The name of the user after passing character validation, without the # part
        ///     For users with claimed names would be the same as DisplayName
        /// </summary>
        public string ValidatedName { get; private set; }

        /// <summary>
        ///     The # part of the name for users without claimed name, will be null for users with a claimed name, includes the # character at the beginning
        /// </summary>
        public string? WalletId { get; private set; }

        /// <summary>
        ///     The name of the user after passing character validation, including the
        ///     Wallet Id (if the user doesnt have a claimed name)
        /// </summary>
        public string DisplayName { get; private set; }
        public string UnclaimedName { get; internal set; }

        /// <summary>
        /// The Display Name with @ before it. Cached here to avoid re-allocations.
        /// </summary>
        public string MentionName => mentionName;

        public bool HasClaimedName
        {
            get => hasClaimedName;

            set
            {
                hasClaimedName = value;
                GenerateAndValidateName();
            }
        }

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

        internal Profile() { }

        internal Profile(string userId, string name, Avatar avatar)
        {
            UserId = userId;
            Name = name;
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
            profile.UserId = userId;
            profile.Name = name;
            profile.Avatar = avatar;
            return profile;
        }

        public void Clear()
        {
            blocked?.Clear();
            interests?.Clear();
            links?.Clear();
            Birthdate = null;
            Avatar.Clear();
            Country = default(string?);
            Email = default(string?);
            Gender = default(string?);
            Description = default(string?);
            Hobbies = default(string?);
            Language = default(string?);
            Profession = default(string?);
            Pronouns = default(string?);
            Version = default(int);
            HasClaimedName = default(bool);
            EmploymentStatus = default(string?);
            UserId = "";
            Name = "";
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

        private void GenerateAndValidateName()
        {
            ValidatedName = string.Empty;
            DisplayName = string.Empty;
            WalletId = null;

            if (string.IsNullOrEmpty(Name)) return;

            string result = string.Empty;
            MatchCollection matches = VALID_NAME_CHARACTERS.Matches(Name);

            foreach (Match match in matches)
                result += match.Value;

            ValidatedName = result;
            DisplayName = result;

            if (!HasClaimedName && !string.IsNullOrEmpty(UserId) && UserId.Length > 4)
            {
                WalletId = $"#{UserId[^4..]}";
                DisplayName = $"{result}{WalletId}";
            }

            mentionName = "@" + DisplayName;
        }

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

            return UserId == profile.UserId
                   && Name == profile.Name
                   && HasClaimedName == profile.HasClaimedName
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
