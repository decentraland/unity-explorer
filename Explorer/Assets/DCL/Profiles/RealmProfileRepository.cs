using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS;
using Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using IpfsProfileEntity = DCL.Ipfs.IpfsRealmEntity<DCL.Profiles.GetProfileJsonRootDto>;

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
        private readonly byte[] whiteTexturePng = Texture2D.whiteTexture.EncodeToPNG();

        public RealmProfileRepository(IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
        }

        public async UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            IIpfsRealm ipfs = realm.Ipfs;

            // TODO: we are not sure if we will need to keep sending snapshots. In the meantime just use white textures
            byte[] faceSnapshotTextureFile = whiteTexturePng;
            byte[] bodySnapshotTextureFile = whiteTexturePng;

            string faceHash = ipfs.GetFileHash(faceSnapshotTextureFile);
            string bodyHash = ipfs.GetFileHash(bodySnapshotTextureFile);

            using var profileDto = GetProfileJsonRootDto.Create();
            profileDto.CopyFrom(profile);
            profileDto.avatars[0].avatar.snapshots.body = bodyHash;
            profileDto.avatars[0].avatar.snapshots.face256 = faceHash;

            var entity = new IpfsProfileEntity
            {
                version = IpfsProfileEntity.DEFAULT_VERSION,
                content = new List<IpfsProfileEntity.Files>
                {
                    new () { file = "body.png", hash = faceHash },
                    new () { file = "face256.png", hash = bodyHash },
                },
                pointers = new List<string> { profile.UserId },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = IpfsRealmEntityType.Profile.ToEntityString(),
                metadata = profileDto,
            };

            files.Clear();
            files[bodyHash] = bodySnapshotTextureFile;
            files[faceHash] = faceSnapshotTextureFile;

            try
            {
                await ipfs.PublishAsync(entity, ct, files);
            }
            finally
            {
                files.Clear();
            }
        }

        public async UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id)) return null;

            Profile? profileInCache = profileCache.Get(id);

            if (profileInCache?.Version > version)
                return profileInCache;

            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            urlBuilder.AppendDomain(ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"))
                      .AppendParameter(new URLParameter("version", version.ToString()));

            URLAddress url = urlBuilder.Build();

            try
            {
                GenericGetRequest response = await webRequestController.GetAsync(new CommonArguments(url), ct);

                using GetProfileJsonRootDto root = await response.CreateFromNewtonsoftJsonAsync<GenericGetRequest, GetProfileJsonRootDto>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, text, exception),
                    serializerSettings: SERIALIZER_SETTINGS);

                if (root.avatars == null) return null;
                if (root.avatars.Count == 0) return null;

                Profile profile = profileInCache ?? new Profile();
                root.avatars[0].CopyTo(profile);
                profileCache.Set(id, profile);

                return profile;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == 404)
                    return null;

                throw;
            }
            finally { urlBuilder.Clear(); }
        }
    }
}
