namespace DCL.WebRequests.ChromeDevtool
{
    internal static class ChromeDevtoolPostData
    {
        private const string NOT_SUPPORTED = "Not Supported";

        public static string? GetPostData<TWebRequest, TWebRequestArgs>(this RequestEnvelope<TWebRequest, TWebRequestArgs> envelope)
            where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            if (typeof(TWebRequestArgs) == typeof(GenericPostArguments) && envelope.args is GenericPostArguments postArguments)
            {
                if (!string.IsNullOrEmpty(postArguments.PostData))
                    return postArguments.PostData;

                if (postArguments.UploadHandler.HasValue)
                    return postArguments.UploadHandler.Value.ToString();

                return NOT_SUPPORTED;
            }

            return null;
        }
    }
}
