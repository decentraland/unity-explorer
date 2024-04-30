using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.Ipfs
{
    public class IpfsRealm : IIpfsRealm, IEquatable<IpfsRealm>
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly List<string> sceneUrns;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, byte[]> files = new ();

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        public IReadOnlyList<string> SceneUrns => sceneUrns;

        public IpfsRealm(IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            URLDomain realmName, ServerAbout? serverAbout = null)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;

            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;

            if (serverAbout != null)
            {
                sceneUrns = serverAbout.configurations.scenesUrn;
                ContentBaseUrl = URLDomain.FromString(serverAbout.content.publicUrl);
                LambdasBaseUrl = URLDomain.FromString(serverAbout.lambdas.publicUrl);

                //Note: Content url requires the subdirectory content, but the actives endpoint requires the base one.
                EntitiesActiveEndpoint = URLBuilder.Combine(ContentBaseUrl, URLSubdirectory.FromString("entities/active"));
                ContentBaseUrl = URLBuilder.Combine(ContentBaseUrl, URLSubdirectory.FromString("contents/"));
            }
            else
            {
                sceneUrns = new List<string>();
                ContentBaseUrl = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/contents/"));
                EntitiesActiveEndpoint = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/entities/active"));
            }
        }

        public bool Equals(IpfsRealm other)
        {
            if (ReferenceEquals(null!, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CatalystBaseUrl == other.CatalystBaseUrl;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null!, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IpfsRealm)obj);
        }

        public override int GetHashCode() =>
            ContentBaseUrl.GetHashCode();

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            var form = NewForm(entity, contentFiles);
            return SendFormAsync(form, ct);
        }

        private WWWForm NewForm<T>(EntityDefinitionGeneric<T> entity, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            string entityJson = JsonUtility.ToJson(entity);
            byte[] entityFile = Encoding.UTF8.GetBytes(entityJson);
            string entityId = GetFileHash(entityFile);
            using AuthChain authChain = web3IdentityCache.Identity!.Sign(entityId);

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

            files.Clear();

            if (contentFiles != null)
                foreach ((string key, byte[] value) in contentFiles)
                    files[key] = value;

            files[entityId] = entityFile;

            foreach ((string hash, byte[] data) in files)
                form.AddBinaryData(hash, data);

            return form;
        }

        private UniTask SendFormAsync(WWWForm form, CancellationToken ct)
        {
            URLAddress url = Url();
            return webRequestController.PostAsync(
                new CommonArguments(url),
                GenericPostArguments.CreateWWWForm(form),
                ct,
                ReportCategory.REALM
            ).WithNoOpAsync();
        }

        private URLAddress Url()
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(CatalystBaseUrl)
                      .AppendPath(URLPath.FromString("content/entities"));

            return urlBuilder.Build();
        }

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
