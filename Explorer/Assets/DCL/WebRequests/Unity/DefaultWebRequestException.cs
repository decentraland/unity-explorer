using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    public class DefaultWebRequestException : WebRequestException
    {
        public DefaultWebRequestException(DefaultWebRequest webRequest, UnityWebRequestException nativeException) : base(webRequest, nativeException)
        {
            ResponseHeaders = webRequest.unityWebRequest.GetResponseHeaders();
        }

        public override Dictionary<string, string>? ResponseHeaders { get; }
    }
}
