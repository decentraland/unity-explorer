using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public interface IProfileRepository
    {
        public const string GUEST_RANDOM_ID = "fakeUserId";
        public const string PLAYER_RANDOM_ID = "Player";

        UniTask SetAsync(Profile profile, bool publish, CancellationToken ct);

        UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct);

        public class Fake : IProfileRepository
        {
            private readonly Dictionary<Key, Profile> profiles = new ();

            public UniTask SetAsync(Profile profile, bool publish, CancellationToken ct)
            {
                var key = new Key(profile);

                if (profiles.TryGetValue(key, out Profile? existingProfile))
                    if (existingProfile != profile)
                        existingProfile.Dispose();

                profiles[key] = profile;

                return UniTask.CompletedTask;
            }

            public async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct)
            {
                var key = new Key(id, version);

                if (profiles.ContainsKey(key) == false)
                    profiles[key] = NewRandomProfile();

                return profiles[key];
            }

            private static Profile NewRandomProfile() =>
                new (
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    new Avatar(
                        new BodyShape(),
                        new HashSet<URN>(),
                        Color.blue,
                        Color.white,
                        Color.black)
                );

            [Serializable]
            private struct Key
            {
                public readonly string ID;
                public readonly int Version;

                public Key(Profile profile) : this(profile.UserId, profile.Version) { }

                public Key(string id, int version)
                {
                    ID = id;
                    Version = version;
                }
            }
        }
    }

    public static class ProfileRepositoryExtensions
    {
        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, CancellationToken ct) =>
            profileRepository.GetAsync(id, 0, null, ct);

        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, int version, CancellationToken ct) =>
            profileRepository.GetAsync(id, version, null, ct);

        public static async UniTask<Profile> EnsuredProfileAsync(this IProfileRepository profileRepository, string id, CancellationToken ct) =>
            (await profileRepository.GetAsync(id, ct)).EnsureNotNull();
    }
}
