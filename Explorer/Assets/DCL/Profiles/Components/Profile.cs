using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Profiles
{
    public class Profile : IDirtyMarker
    {
        private static readonly Regex VALID_NAME_CHARACTERS = new ("[a-zA-Z0-9]");

        internal HashSet<string>? blocked;
        internal List<string>? interests;
        internal List<string>? links;

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
        public string Description { get; internal set; }
        public int TutorialStep { get; internal set; }
        public string Email { get; internal set; }
        public string Country { get; internal set; }
        public string EmploymentStatus { get; internal set; }
        public string Gender { get; internal set; }
        public string Pronouns { get; internal set; }
        public string RelationshipStatus { get; internal set; }
        public string SexualOrientation { get; internal set; }
        public string Language { get; internal set; }
        public string Profession { get; internal set; }
        public string RealName { get; internal set; }
        public string Hobbies { get; internal set; }
        public DateTime? Birthdate { get; internal set; }
        public int Version { get; internal set; }
        public Avatar Avatar { get; internal set; }

        /// <summary>
        ///     This flag can be moved elsewhere when the final flow is established
        /// </summary>
        public bool IsDirty { get; set; } = true;

        public IReadOnlyCollection<string>? Blocked => blocked;
        public IReadOnlyCollection<string>? Interests => interests;
        public IReadOnlyCollection<string>? Links => links;

        public Profile() { }

        public Profile(string userId, string name, Avatar avatar)
        {
            UserId = userId;
            Name = name;
            Avatar = avatar;
        }

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
    }
}
