using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public class ProfileRepositoryFake : IProfileRepository
    {
        private readonly Dictionary<Key, Profile> profiles = new ();

        public UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            var key = new Key(profile);

            if (profiles.TryGetValue(key, out Profile? existingProfile))
                if (existingProfile != profile)
                    existingProfile.Dispose();

            profiles[key] = profile;

            return UniTask.CompletedTask;
        }

        public UniTask<List<Profile>> GetAsync(IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null)
        {
            var results = new List<Profile>(ids.Count);

            foreach (string id in ids)
            {
                var key = new Key(id, 0);

                if (!profiles.ContainsKey(key))
                    profiles[key] = NewRandomProfile();

                results.Add(profiles[key]);
            }

            return UniTask.FromResult(results);
        }

        public UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT, IPartitionComponent? partition = null, RetryPolicy? retryPolicy = null)
        {
            var key = new Key(id, version);

            if (profiles.ContainsKey(key) == false)
                profiles[key] = NewRandomProfile();

            return UniTask.FromResult(profiles[key])!;
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
