using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public partial class RealmProfileRepository : IProfileRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new ProfileJsonRootDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly URLBuilder urlBuilder = new ();

        public RealmProfileRepository(IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            var profileDto = GetProfileJsonRootDto.Create();
            profileDto.CopyFrom(profile);

            var entity = new Entity
            {
                version = "v3",
                content = new List<Entity.Files>(),
                pointers = new List<string> { web3IdentityCache.Identity!.Address },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = "profile",
                metadata = profileDto,
            };

            string entityJson = JsonUtility.ToJson(entity);
            byte[] entityFile = Encoding.UTF8.GetBytes(entityJson);
            string entityId = HashV1(entityFile);
            AuthChain authChain = web3IdentityCache.Identity!.Sign(entityId);

            var form = new WWWForm();

            form.AddField("entityId", entityId);

            var i = 0;

            foreach (AuthLink link in authChain)
            {
                form.AddField($"authChain[{i}][type]", link.type.ToString());
                form.AddField($"authChain[{i}][payload]", link.payload);
                form.AddField($"authChain[{i}][signature]", link.signature ?? "");
                i++;
            }

            form.AddBinaryData(entityId, entityFile);

            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            URLAddress url = urlBuilder.AppendDomain(ipfs.CatalystBaseUrl)
                                       .AppendPath(URLPath.FromString("content/entities"))
                                       .Build();

            try
            {
                await webRequestController.PostAsync(new CommonArguments(url),
                    GenericPostArguments.CreateWWWForm(form), ct);
            }
            catch (Exception e)
            {
                throw;
            }
            finally { profileDto.Dispose(); }
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
        }

        private string HashV1(byte[] content)
        {
            // Result from hashV1(new TextEncoder().encode('bleh'))
            // var cid = Cid.Decode("bafkreifqbsjvx7fa34z5md73yoi6z7jktxl35e3ga6pvjfansr2ehyx6aq");

            var sha2256MultiHash = MultiHash.ComputeHash(content);
            var cid = new Cid { Encoding = "base32", ContentType = "raw", Hash = sha2256MultiHash, Version = 1 };
            var hash = cid.ToString();
            return hash;
        }

        [Serializable]
        private struct Entity
        {
            [Serializable]
            public struct Files
            {
                public string file;
                public string hash;
            }

            public string version;
            public string type;
            public List<string> pointers;
            public long timestamp;
            public GetProfileJsonRootDto metadata;
            public List<Files> content;
        }
    }
}
