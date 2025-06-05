using Best.HTTP;
using System.Net.Http;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericPatchRequest : GenericUploadRequestBase
    {
        internal GenericPatchRequest(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            CreateHttp2Request(HTTPMethods.Post);

        public override UnityWebRequest CreateUnityWebRequest() =>
            CreateUnityWebRequest("PATCH");

        public override HttpRequestMessage CreateYetAnotherHttpRequest() =>
            CreateYetAnotherHttpRequest(HttpMethod.Patch);
    }
}
