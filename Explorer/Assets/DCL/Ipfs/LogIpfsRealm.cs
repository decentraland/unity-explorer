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

        public LogIpfsRealm(IIpfsRealm origin)
        {
            this.origin = origin;
        }

        public URLDomain CatalystBaseUrl
        {
            get
            {
                var result = origin.CatalystBaseUrl;
                ReportHub
                    .WithReport(ReportCategory.REALM)
                    .Log($"IpfsRealm CatalystBaseUrl requested, result: {result}");
                return result;
            }
        }

        public URLDomain ContentBaseUrl
        {
            get
            {
                var result = origin.ContentBaseUrl;
                ReportHub
                    .WithReport(ReportCategory.REALM)
                    .Log($"IpfsRealm ContentBaseUrl requested, result: {result}");
                return result;
            }
        }

        public URLDomain LambdasBaseUrl
        {
            get
            {
                var result = origin.LambdasBaseUrl;
                ReportHub
                    .WithReport(ReportCategory.REALM)
                    .Log($"IpfsRealm LambdasBaseUrl requested, result: {result}");
                return result;
            }
        }

        public IReadOnlyList<string> SceneUrns
        {
            get
            {
                var result = origin.SceneUrns;
                ReportHub
                    .WithReport(ReportCategory.REALM)
                    .Log($"IpfsRealm SceneUrns requested, result: {string.Join(", ", result)}");
                return result;
            }
        }

        public URLDomain EntitiesActiveEndpoint
        {
            get
            {
                var result = origin.EntitiesActiveEndpoint;
                ReportHub
                    .WithReport(ReportCategory.REALM)
                    .Log($"IpfsRealm EntitiesActiveEndpoint requested, result: {result}");
                return result;
            }
        }

        public async UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("IpfsRealm PublishAsync requested");
            sb.AppendLine($"Entity: {entity.FullInfo()}");
            sb.AppendLine($"Content files: {string.Join(", ", contentFiles?.Keys ?? Array.Empty<string>())}");
            ReportHub
                .WithReport(ReportCategory.REALM)
                .Log(sb.ToString());
            await origin.PublishAsync(entity, ct, contentFiles);
        }

        public string GetFileHash(byte[] file)
        {
            string result = origin.GetFileHash(file);
            ReportHub
                .WithReport(ReportCategory.REALM)
                .Log($"IpfsRealm GetFileHash requested, result: {result}");
            return result;
        }
    }
}
