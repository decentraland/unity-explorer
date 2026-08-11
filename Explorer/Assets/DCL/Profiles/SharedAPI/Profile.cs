using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.Optimization.ThreadSafePool;
using DCL.Utility.Types;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace DCL.Profiles
{
    public partial class Profile : IDirtyMarker, IDisposable
    {
        internal HashSet<string>? blocked;
        internal List<string>? interests;
        internal List<LinkJsonDto>? links;

        public StreamableLoadingResult<SpriteData>.WithFallback? ProfilePicture
        {
            get => compact.ProfilePicture;
            set => GetCompact().ProfilePicture = value;
        }

        public AssetPromise<TextureData, GetTextureIntention>? PicturePromise
        {
            get => compact.PicturePromise;
            set => GetCompact().PicturePromise = value;
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
        public int Version { get; set; }
        public Avatar Avatar { get; set; }

        /// <summary>
        ///     This flag can be moved elsewhere when the final flow is established
        /// </summary>
        public bool IsDirty { get; set; }

        public IReadOnlyCollection<string>? Blocked => blocked;
        public IReadOnlyCollection<string>? Interests => interests;

        public List<LinkJsonDto>? Links
        {
            get => links;
            set => links = value;
        }

        private CompactInfo compact;

        public CompactInfo Compact => compact;

        public Profile(UserId userId, string name, Avatar avatar)
        {
            compact = new CompactInfo(userId, name);
            Avatar = avatar;
        }

        /// <summary>
        ///     Wraps an already validated <see cref="CompactInfo" />, used by deserialization
        ///     which parses the compact slice before the rest of the profile.
        /// </summary>
        internal Profile(in CompactInfo compact)
        {
            this.compact = compact;
            Avatar = new Avatar();
        }

        public void Dispose()
        {
            GetCompact().Dispose();

            if (blocked != null)
            {
                ThreadSafeCollectionPool<HashSet<string>, string>.SHARED.Release(blocked);
                blocked = null;
            }

            if (interests != null)
            {
                ThreadSafeCollectionPool<List<string>, string>.SHARED.Release(interests);
                interests = null;
            }

            if (links != null)
            {
                ThreadSafeCollectionPool<List<LinkJsonDto>, LinkJsonDto>.SHARED.Release(links);
                links = null;
            }
        }

        public static Profile NewRandomProfile(string? userId)
        {
            BodyShape bodyShape = Random.value > 0.5f ? BodyShape.MALE : BodyShape.FEMALE;

            return new Profile(
                userId: UserIdOrGuest(userId),
                name: IProfileRepository.PLAYER_RANDOM_ID,
                avatar: new Avatar(
                    bodyShape,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(bodyShape),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor()
                )
            );
        }

        public static Profile NewProfileWithAvatar(string? userId, Avatar avatar) =>
            new (
                UserIdOrGuest(userId),
                IProfileRepository.PLAYER_RANDOM_ID,
                avatar
            );

        private static UserId UserIdOrGuest(string? raw)
        {
            Option<UserId> userId = UserId.New(raw);

            if (userId.Has)
                return userId.Value;

            Option<UserId> guest = UserId.New(IProfileRepository.GUEST_RANDOM_ID);

            if (!guest.Has)
                throw new InvalidOperationException($"{nameof(IProfileRepository.GUEST_RANDOM_ID)} must be a non-empty constant");

            return guest.Value;
        }

        public void ClearLinks()
        {
            if (links == null)
                links = ThreadSafeCollectionPool<List<LinkJsonDto>, LinkJsonDto>.SHARED.Get();
            else
                links.Clear();
        }

        public bool IsSameProfile(Profile profile)
        {
            if (!Avatar.IsSameAvatar(profile.Avatar)) return false;

            return Compact.Equals(profile.Compact)
                   && HasConnectedWeb3 == profile.HasConnectedWeb3
                   && AreStringsEquivalent(Description, profile.Description)
                   && TutorialStep == profile.TutorialStep
                   && AreStringsEquivalent(Email, profile.Email)
                   && AreStringsEquivalent(Country, profile.Country)
                   && AreStringsEquivalent(EmploymentStatus, profile.EmploymentStatus)
                   && AreStringsEquivalent(Gender, profile.Gender)
                   && AreStringsEquivalent(Pronouns, profile.Pronouns)
                   && AreStringsEquivalent(RelationshipStatus, profile.RelationshipStatus)
                   && AreStringsEquivalent(SexualOrientation, profile.SexualOrientation)
                   && AreStringsEquivalent(Language, profile.Language)
                   && AreStringsEquivalent(Profession, profile.Profession)
                   && AreStringsEquivalent(RealName, profile.RealName)
                   && AreStringsEquivalent(Hobbies, profile.Hobbies)
                   && Birthdate == profile.Birthdate
                   && Version == profile.Version
                   && AreLinksSame(links, profile.links)
                   && ClaimedNameColor == profile.ClaimedNameColor;
        }

        private static bool AreStringsEquivalent(string? a, string? b) =>
            (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) || a == b;

        private static bool AreLinksSame(List<LinkJsonDto>? links1, List<LinkJsonDto>? links2)
        {
            if (links1 == null && links2 == null) return true;
            if (links1 == null || links2 == null) return false;
            if (links1.Count != links2.Count) return false;

            for (int i = 0; i < links1.Count; i++)
            {
                if (links1[i].title != links2[i].title || links1[i].url != links2[i].url)
                    return false;
            }
            return true;
        }
    }
}
