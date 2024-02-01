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
using UnityEngine.Networking;

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
        private readonly List<IMultipartFormSection> multipartFormSections = new ();

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
            // string result = Base32.Encode(Encoding.UTF8.GetBytes("bleh"));
            // List<string> bleh = new List<string>();
            //
            // foreach (var hashingAlgorithm in HashingAlgorithm.All)
            // {
            //     MultiHash hash = MultiHash.ComputeHash(Encoding.UTF8.GetBytes("bleh"), hashingAlgorithm.Name);
            //     bleh.Add(hash.ToString());
            //     bleh.Add(hash.ToBase32());
            //     bleh.Add(hash.ToBase58());
            // }

            // using (var ms = new MemoryStream(MultiBase.Decode(input), false))
            // {
            //     var v = ms.ReadVarint32();
            //     if (v != 1)
            //     {
            //         throw new InvalidDataException($"Unknown CID version '{v}'.");
            //     }
            //     return new Cid
            //     {
            //         Version = v,
            //         Encoding = Registry.MultiBaseAlgorithm.Codes[input[0]].Name,
            //         ContentType = ms.ReadMultiCodec().Name,
            //         Hash = new MultiHash(ms)
            //     };
            // }

            var hash = MultiHash.ComputeHash(Encoding.UTF8.GetBytes("bleh"));

            // string base32 = hash.ToBase32();
            // string hashStr = hash.ToString();
            // Cid cid = Cid.Decode(hashStr);
            // Cid cid = hash;
            // string s = cid.ToString();

            // Result from hashV1(new TextEncoder().encode('bleh'))
            var cid = Cid.Decode("bafkreifqbsjvx7fa34z5md73yoi6z7jktxl35e3ga6pvjfansr2ehyx6aq");

            return;

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

            byte[] entityFile = Encoding.UTF8.GetBytes(JsonUtility.ToJson(entity));
            string entityId = Base32.Encode(entityFile);
            AuthChain authChain = web3IdentityCache.Identity!.Sign(entityId);

            multipartFormSections.Clear();

            multipartFormSections.Add(new MultipartFormDataSection("entityId", entityId));

            var i = 0;

            foreach (AuthLink link in authChain)
            {
                multipartFormSections.Add(new MultipartFormDataSection($"authChain[{i}][type]", link.type.ToString()));
                multipartFormSections.Add(new MultipartFormDataSection($"authChain[{i}][payload]", link.payload));
                multipartFormSections.Add(new MultipartFormDataSection($"authChain[{i}][signature]", link.signature));
                i++;
            }

            multipartFormSections.Add(new MultipartFormDataSection(entityId, entityFile));

            IIpfsRealm ipfs = realm.Ipfs;

            urlBuilder.Clear();

            URLAddress url = urlBuilder.AppendDomain(ipfs.ContentBaseUrl)
                                       .AppendPath(URLPath.FromString("entities"))
                                       .Build();

            try { await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateMultipartForm(multipartFormSections), ct); }
            finally
            {
                multipartFormSections.Clear();
                profileDto.Dispose();
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
