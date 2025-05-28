using DCL.Diagnostics;
using System;
using Utility.Times;

namespace DCL.WebRequests
{
    public static class SignedWebRequestControllerExtensions
    {
        public static GenericPostRequest SignedFetchPostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string signatureMetadata,
            ReportData reportData
        ) =>
            SignedFetchPostAsync(controller, commonArguments, GenericUploadArguments.Empty, signatureMetadata, reportData);

        public static GenericPostRequest SignedFetchPostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments arguments,
            string signatureMetadata,
            ReportData reportData
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PostAsync(
                commonArguments,
                arguments,
                reportData,
                new WebRequestHeadersInfo().WithSign(signatureMetadata, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(signatureMetadata, commonArguments.URL, unixTimestamp, "post")
            );
        }

        public static GenericGetRequest SignedFetchGetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            ReportData reportData
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.GetAsync(
                commonArguments,
                reportData,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "get"));
        }

        public static GenericDeleteRequest SignedFetchDeleteAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            ReportData reportData
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.DeleteAsync(
                commonArguments,
                GenericUploadArguments.Empty,
                reportData,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "delete")
            );
        }

        public static GenericPatchRequest SignedFetchPatchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments patchArguments,
            string jsonMetaData,
            ReportData reportData)
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PatchAsync(
                commonArguments,
                patchArguments,
                reportData,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "patch")
            );
        }

        public static GenericDownloadHandlerUtils.Adapter<GenericPutRequest, GenericPutArguments> SignedFetchPutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPutArguments putArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new GenericDownloadHandlerUtils.Adapter<GenericPutRequest, GenericPutArguments>(
                controller,
                commonArguments,
                putArguments,
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "put"),
                null
            );
        }
    }
}
