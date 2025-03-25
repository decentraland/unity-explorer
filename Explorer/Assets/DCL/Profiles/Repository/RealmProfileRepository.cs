using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.UI.Profiles.Helpers;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using IpfsProfileEntity = DCL.Ipfs.EntityDefinitionGeneric<DCL.Profiles.GetProfileJsonRootDto>;

namespace DCL.Profiles
{
    public partial class RealmProfileRepository : IProfileRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new ProfileJsonRootDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly IProfileCache profileCache;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, byte[]> files = new ();
        // Catalyst servers requires a face thumbnail texture of 256x256
        // Otherwise it will fail when the profile is published
        private readonly byte[] whiteTexturePng = new Texture2D(256, 256).EncodeToPNG();

        public RealmProfileRepository(
            IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
        }

        public async UniTask SetAsync(Profile profile, bool publish, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(profile.UserId))
                throw new ArgumentException("Can't set a profile with an empty UserId");

            IIpfsRealm ipfs = realm.Ipfs;

            // TODO: we are not sure if we will need to keep sending snapshots. In the meantime just use white textures
            byte[] faceSnapshotTextureFile = whiteTexturePng;
            byte[] bodySnapshotTextureFile = whiteTexturePng;

            string faceHash = ipfs.GetFileHash(faceSnapshotTextureFile);
            string bodyHash = ipfs.GetFileHash(bodySnapshotTextureFile);

            using var profileDto = NewProfileJsonRootDto(profile, bodyHash, faceHash);
            var entity = NewPublishProfileEntity(profile, profileDto, bodyHash, faceHash);

            files.Clear();
            files[bodyHash] = bodySnapshotTextureFile;
            files[faceHash] = faceSnapshotTextureFile;

            try
            {
                if (publish)
                    await ipfs.PublishAsync(entity, ct, files);
                profileCache.Set(profile.UserId, profile);
            }
            finally { files.Clear(); }
        }

        private static GetProfileJsonRootDto NewProfileJsonRootDto(Profile profile, string bodyHash, string faceHash)
        {
            var profileDto = GetProfileJsonRootDto.Create();
            profileDto.CopyFrom(profile);
            profileDto.avatars[0]!.avatar.snapshots.body = bodyHash;
            profileDto.avatars[0]!.avatar.snapshots.face256 = faceHash;
            return profileDto;
        }

        private static IpfsProfileEntity NewPublishProfileEntity(Profile profile, GetProfileJsonRootDto profileJsonRootDto, string bodyHash, string faceHash) =>
            new (string.Empty, profileJsonRootDto)
            {
                version = IpfsProfileEntity.DEFAULT_VERSION,
                content = new List<ContentDefinition>
                {
                    new () { file = "body.png", hash = bodyHash },
                    new () { file = "face256.png", hash = faceHash },
                },
                pointers = new List<string> { profile.UserId },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = IpfsRealmEntityType.Profile.ToEntityString(),
            };

        public async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (TryProfileFromCache(id, version, out Profile? profileInCache)) return profileInCache;

            Assert.IsTrue(realm.Configured, "Can't get profile if the realm is not configured");

            try
            {
                URLAddress url = Url(id, fromCatalyst);
                GenericGetRequest response = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM, ignoreErrorCodes: IWebRequestController.IGNORE_NOT_FOUND);

                using GetProfileJsonRootDto? root = await response.CreateFromNewtonsoftJsonAsync<GetProfileJsonRootDto>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, text, exception),
                    serializerSettings: SERIALIZER_SETTINGS);

                var profileDto = root?.FirstProfileDto();

                if (profileDto is null)
                    return null;

                // Reusing the profile in cache does not allow other systems to properly update.
                // It impacts on the object state and does not allow to make comparisons on change.
                // For example the multiplayer system, whenever a remote profile update comes in,
                // it compares the version of the profile to check if it has changed. By overriding the version here,
                // the check always fails. So its necessary to get a new instance each time
                Profile profile = Profile.Create();
                profileDto.CopyTo(profile);
                profile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);

                profileCache.Set(id, profile);

                return profile;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == WebRequestUtils.NOT_FOUND)
                    return null;

                throw;
            }
        }

        private URLAddress Url(string id, URLDomain? fromCatalyst)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(fromCatalyst ?? realm.Ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"));

            return urlBuilder.Build();
        }

        private bool TryProfileFromCache(string id, int version, out Profile? profile)
        {
            profile = profileCache.Get(id);

            if (profile == null)
                return false;

            return profile.Version >= version;
        }
    }
}
