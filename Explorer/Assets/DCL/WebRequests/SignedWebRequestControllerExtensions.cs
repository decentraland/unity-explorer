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

        public static GetTextureWebRequest SignedFetchTextureAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GetTextureArguments args,
            string jsonMetaData,
            ReportData reportData)
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.GetTextureAsync(
                commonArguments,
                args,
                reportData,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "get")
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

        public static GenericPutRequest SignedFetchPutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericUploadArguments putArguments,
            string jsonMetaData,
            ReportData reportData)
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return controller.PutAsync(
                commonArguments,
                putArguments,
                reportData,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "put")
            );
        }
    }
}
