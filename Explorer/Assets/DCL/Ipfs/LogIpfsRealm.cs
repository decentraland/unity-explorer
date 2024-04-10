using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DCL.Ipfs
{
    public class LogIpfsRealm : IIpfsRealm
    {
        private readonly IIpfsRealm origin;
        private readonly Action<string> log;

        public LogIpfsRealm(IIpfsRealm origin) : this(origin, ReportHub.WithReport(ReportCategory.REALM).Log) { }

        public LogIpfsRealm(IIpfsRealm origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public URLDomain CatalystBaseUrl
        {
            get
            {
                var result = origin.CatalystBaseUrl;
                log($"IpfsRealm CatalystBaseUrl requested, result: {result}");
                return result;
            }
        }

        public URLDomain ContentBaseUrl
        {
            get
            {
                var result = origin.ContentBaseUrl;
                log($"IpfsRealm ContentBaseUrl requested, result: {result}");
                return result;
            }
        }

        public URLDomain LambdasBaseUrl
        {
            get
            {
                var result = origin.LambdasBaseUrl;
                log($"IpfsRealm LambdasBaseUrl requested, result: {result}");
                return result;
            }
        }

        public IReadOnlyList<string> SceneUrns
        {
            get
            {
                var result = origin.SceneUrns;
                log($"IpfsRealm SceneUrns requested, result: {string.Join(", ", result)}");
                return result;
            }
        }

        public URLDomain EntitiesActiveEndpoint
        {
            get
            {
                var result = origin.EntitiesActiveEndpoint;
                log($"IpfsRealm EntitiesActiveEndpoint requested, result: {result}");
                return result;
            }
        }

        public async UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("IpfsRealm PublishAsync requested");
            sb.AppendLine($"Entity: {entity}");
            sb.AppendLine($"Content files: {string.Join(", ", contentFiles?.Keys ?? Array.Empty<string>())}");
            log(sb.ToString());
            await origin.PublishAsync(entity, ct, contentFiles);
        }

        public string GetFileHash(byte[] file)
        {
            string result = origin.GetFileHash(file);
            log($"IpfsRealm GetFileHash requested, result: {result}");
            return result;
        }
    }
}
