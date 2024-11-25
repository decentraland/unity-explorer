using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.GenericDelete;
using System;
using System.Threading;
using Utility.Times;
using static DCL.WebRequests.WebRequestControllerExtensions;

namespace DCL.WebRequests
{
    public static class SignedWebRequestControllerExtensions
    {
        public static UniTask<TResult> SignedFetchPostAsync<TOp, TResult>(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            TOp webRequestOp,
            string signatureMetadata,
            ReportData reportData,
            CancellationToken ct
        )
            where TOp: struct, IWebRequestOp<GenericPostRequest, TResult>
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PostAsync<TOp, TResult>(
                commonArguments,
                webRequestOp,
                GenericPostArguments.Empty,
                ct,
                reportData,
                signInfo: WebRequestSignInfo.NewFromRaw(signatureMetadata, commonArguments.URL, unixTimestamp, "post"),
                headersInfo: new WebRequestHeadersInfo().WithSign(signatureMetadata, unixTimestamp)
            );
        }

        public static GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> SignedFetchPostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments>(
                controller,
                commonArguments,
                GenericPostArguments.Empty,
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "post"),
                null,
                POST_GENERIC
            );
        }

        public static GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> SignedFetchPostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPostArguments postArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments>(
                controller,
                commonArguments,
                postArguments,
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "post"),
                null,
                POST_GENERIC
            );
        }

        public static GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> SignedFetchGetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments>(
                controller,
                commonArguments,
                new GenericGetArguments(),
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "get"),
                null,
                GET_GENERIC
            );
        }

        public static GenericDownloadHandlerUtils.Adapter<GenericDeleteRequest, GenericDeleteArguments> SignedFetchDeleteAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new GenericDownloadHandlerUtils.Adapter<GenericDeleteRequest, GenericDeleteArguments>(
                controller,
                commonArguments,
                GenericDeleteArguments.Empty,
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "delete"),
                null,
                DELETE_GENERIC
            );
        }
    }
}
