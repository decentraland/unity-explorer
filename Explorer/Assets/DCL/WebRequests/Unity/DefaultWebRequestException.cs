using System;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    public class DefaultWebRequestException : WebRequestException
    {
        public DefaultWebRequestException(DefaultWebRequest webRequest, Exception nativeException) : base(webRequest, nativeException)
        {
            ResponseHeaders = webRequest.unityWebRequest.GetResponseHeaders();
        }

        public override Dictionary<string, string>? ResponseHeaders { get; }
    }
}
