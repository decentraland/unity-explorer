using DCL.Diagnostics;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Contains all possible parameters needed to create a web request
    /// </summary>
    /// <typeparam name="TWebRequestArgs"></typeparam>
    public readonly struct RequestEnvelope : IDisposable
    {
        public readonly ReportData ReportData;
        public readonly CommonArguments CommonArguments;
        public readonly bool SuppressErrors;

        public readonly WebRequestHeadersInfo HeadersInfo;
        public readonly WebRequestSignInfo? SignInfo;

        /// <summary>
        ///     Allows to hook into the web request creation and observe/manipulate its state during the execution of the process (before it's fully complete)
        /// </summary>
        public readonly Action<IWebRequest>? OnCreated;

        private const string NONE = "NONE";

        public RequestEnvelope(
            CommonArguments commonArguments,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            bool suppressErrors = false,
            Action<IWebRequest>? onCreated = null)
        {
            this.CommonArguments = commonArguments;
            ReportData = reportData;
            HeadersInfo = headersInfo ?? WebRequestHeadersInfo.NewEmpty();
            SignInfo = signInfo;
            SuppressErrors = suppressErrors;
            OnCreated = onCreated;
        }

        public override string ToString() =>
            "RequestEnvelope:"
            + $"\nCommonArguments: {CommonArguments}"
            + $"\nReportCategory: {ReportData}"
            + $"\nHeaders: {HeadersInfo.ToString()}"
            + $"\nSignInfo: {SignInfo?.ToString() ?? NONE}";

        internal void InitializedWebRequest(IWeb3IdentityCache web3IdentityCache, IWebRequest webRequest)
        {
            AssignTimeout(webRequest);
            AssignHeaders(webRequest, web3IdentityCache);
        }

        /// <summary>
        ///     A simplified representation of the request envelope:
        ///     URL
        ///     Headers: {headers} if present
        ///     SignInfo: {signInfo} if present
        /// </summary>
        /// <returns></returns>
        public string GetBreadcrumbString(StringBuilder sb)
        {
            sb.Clear();

            sb.AppendLine(CommonArguments.ToString());

            if (HeadersInfo.Value.Count != 0)
            {
                sb.Append("Headers: ");
                sb.AppendLine(HeadersInfo.ToString());
            }

            if (SignInfo.HasValue)
            {
                sb.Append("SignInfo: ");
                sb.AppendLine(SignInfo.Value.ToString());
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
            HeadersInfo.Dispose();
        }

        private void AssignHeaders(IWebRequest webRequest, IWeb3IdentityCache web3IdentityCache)
        {
            SignRequest(webRequest, web3IdentityCache);
            SetHeaders(webRequest);
        }

        private void AssignTimeout(IWebRequest webRequest)
        {
            webRequest.SetTimeout(CommonArguments.Timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetHeaders(IWebRequest unityWebRequest)
        {
            var info = HeadersInfo.Value;
            int count = info.Count;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < count; i++)
            {
                WebRequestHeader header = info[i];

                try { unityWebRequest.SetRequestHeader(header.Name, header.Value); }
                catch (InvalidOperationException e) { throw new Exception($"Cannot set header: {header.Name} - {header.Value}", e); }
            }
        }

        internal void SignRequest(IWebRequest webRequest, IWeb3IdentityCache web3IdentityCache)
        {
            if (SignInfo.HasValue == false)
                return;

            using AuthChain authChain = web3IdentityCache.EnsuredIdentity().Sign(SignInfo.Value.StringToSign);

            var i = 0;
#if DEBUG
            var sb = new StringBuilder();
#endif

            foreach (AuthLink link in authChain)
            {
                string name = AuthChainHeaderNames.Get(i);
                string value = link.ToJson();
                webRequest.SetRequestHeader(name, value);
#if DEBUG
                sb.AppendLine($"Header {name}: {value}");
#endif
                i++;
            }
#if DEBUG
            ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, sb);
#endif
        }
    }

    /// <remarks>
    ///     Because <see cref="RequestEnvelope{TWebRequest,TWebRequestArgs}" /> is generic, we have
    ///     to put this out here, else we get a copy for every specific type of it we create.
    /// </remarks>
    internal static class AuthChainHeaderNames
    {
        private static readonly string[] AUTH_CHAIN_HEADER_NAMES;

        static AuthChainHeaderNames()
        {
            int maxAuthChainHeaders = Enum.GetNames(typeof(AuthLinkType)).Length;
            AUTH_CHAIN_HEADER_NAMES = new string[maxAuthChainHeaders];

            for (var i = 0; i < maxAuthChainHeaders; i++)
                AUTH_CHAIN_HEADER_NAMES[i] = $"x-identity-auth-chain-{i}";
        }

        public static string Get(int index) =>
            AUTH_CHAIN_HEADER_NAMES[index];
    }
}
