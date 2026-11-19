using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.Ipfs
{
    public class PublishIpfsEntityCommand
    {
        private readonly Dictionary<string, byte[]> files = new ();

        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public PublishIpfsEntityCommand(IWeb3IdentityCache web3IdentityCache, IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask ExecuteAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, JsonSerializerSettings? serializerSettings = null, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            (WWWForm form, string entityJson) data = default;

            try
            {
                data = NewForm(entity, serializerSettings, contentFiles);
                await SendFormAsync(data.form, ct);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Entity {typeof(T).Name} publishing failed:\n{urlsSource.Url(DecentralandUrl.EntitiesDeployment)}\n{e.Message}\n{data.entityJson}");
                throw;
            }
        }

        /// <summary>
        ///     TODO convert this WWWForm to MultiForm API
        /// </summary>
        private (WWWForm form, string entityJson) NewForm<T>(EntityDefinitionGeneric<T> entity, JsonSerializerSettings? serializerSettings, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            string entityJson = JsonConvert.SerializeObject(entity, Formatting.None, serializerSettings);
            byte[] entityFile = Encoding.UTF8.GetBytes(entityJson);
            // Content-addressed: the id does not depend on the realm, which can be unset mid realm change
            string entityId = entityFile.IpfsHashV1();
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

            return (form, entityJson);
        }

        private UniTask SendFormAsync(WWWForm form, CancellationToken ct) =>

            //Added an attempts delay to allow a retry after 2 seconds in order
            //to reduce the chances of parallel profiles deployments
            webRequestController.PostAsync(
                                     new CommonArguments(URLAddress.FromString(urlsSource.Url(DecentralandUrl.EntitiesDeployment)), RetryPolicy.Enforce()),
                                     GenericPostArguments.CreateWWWForm(form),
                                     ct,
                                     ReportCategory.REALM
                                 )
                                .WithNoOpAsync();
    }
}
