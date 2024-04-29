using DCL.Diagnostics;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct RequestEnvelope<TWebRequest, TWebRequestArgs> : IDisposable where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct
    {
        public readonly string ReportCategory;
        public readonly CommonArguments CommonArguments;
        public readonly CancellationToken Ct;
        private readonly InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest;
        private readonly TWebRequestArgs args;
        private readonly WebRequestHeadersInfo headersInfo;
        private readonly WebRequestSignInfo? signInfo;
        private readonly ISet<long>? responseCodeIgnores;

        private const string NONE = "NONE";

        public RequestEnvelope(
            InitializeRequest<TWebRequestArgs, TWebRequest> initializeRequest,
            CommonArguments commonArguments, TWebRequestArgs args,
            CancellationToken ct,
            string reportCategory,
            WebRequestHeadersInfo headersInfo,
            WebRequestSignInfo? signInfo,
            ISet<long>? responseCodeIgnores = null
        )
        {
            this.initializeRequest = initializeRequest;
            this.CommonArguments = commonArguments;
            this.args = args;
            this.Ct = ct;
            this.ReportCategory = reportCategory;
            this.headersInfo = headersInfo;
            this.signInfo = signInfo;
            this.responseCodeIgnores = responseCodeIgnores;
        }

        public override string ToString() =>
            "RequestEnvelope:"
            + $"\nWebRequestType: {typeof(TWebRequest).Name}"
            + $"\nWebRequestArgs: {typeof(TWebRequest).Name}"
            + $"\nCommonArguments: {CommonArguments}"
            + $"\nArgs: {args}"
            + $"\nCancellation Token cancelled: {Ct.IsCancellationRequested}"
            + $"\nReportCategory: {ReportCategory}"
            + $"\nHeaders: {headersInfo.ToString()}"
            + $"\nSignInfo: {signInfo?.ToString() ?? NONE}";

        public TWebRequest InitializedWebRequest(IWeb3IdentityCache web3IdentityCache)
        {
            var request = initializeRequest(CommonArguments, args);
            UnityWebRequest unityWebRequest = request.UnityWebRequest;

            AssignTimeout(unityWebRequest);
            AssignHeaders(unityWebRequest, web3IdentityCache);
            AssignDownloadHandler(unityWebRequest);

            return request;
        }

        public void Dispose()
        {
            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
            headersInfo.Dispose();
        }

        public bool ShouldIgnoreResponseError(UnityWebRequest webRequest)
        {
            if (webRequest.result is UnityWebRequest.Result.Success)
                return true;

            return responseCodeIgnores?.Contains(webRequest.responseCode) ?? false;
        }

        private void AssignHeaders(UnityWebRequest unityWebRequest, IWeb3IdentityCache web3IdentityCache)
        {
            SignRequest(unityWebRequest, web3IdentityCache);
            SetHeaders(unityWebRequest);
        }

        private void AssignDownloadHandler(UnityWebRequest unityWebRequest)
        {
            if (CommonArguments.CustomDownloadHandler != null)
                unityWebRequest.downloadHandler = CommonArguments.CustomDownloadHandler;
        }

        private void AssignTimeout(UnityWebRequest unityWebRequest)
        {
            unityWebRequest.timeout = CommonArguments.Timeout;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetHeaders(UnityWebRequest unityWebRequest)
        {
            var info = headersInfo.Value;
            int count = info.Count;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < count; i++)
            {
                WebRequestHeader header = info[i];
                unityWebRequest.SetRequestHeader(header.Name, header.Value);
            }
        }

        private void SignRequest(UnityWebRequest unityWebRequest, IWeb3IdentityCache web3IdentityCache)
        {
            if (signInfo.HasValue == false)
                return;

            using AuthChain authChain = web3IdentityCache.EnsuredIdentity().Sign(signInfo.Value.StringToSign);

            var i = 0;
#if DEBUG
            var sb = new StringBuilder();
#endif

            foreach (AuthLink link in authChain)
            {
                var name = $"x-identity-auth-chain-{i}";
                string value = link.ToJson();
                unityWebRequest.SetRequestHeader(name, value);
#if DEBUG
                sb.AppendLine($"Header {name}: {value}");
#endif
                i++;
            }
#if DEBUG
            ReportHub.Log(Diagnostics.ReportCategory.GENERIC_WEB_REQUEST, sb);
#endif
        }
    }
}
