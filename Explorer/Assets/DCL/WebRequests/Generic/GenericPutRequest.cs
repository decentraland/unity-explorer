using Best.HTTP;
using System.Net.Http;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericPutRequest : GenericUploadRequestBase
    {
        internal GenericPutRequest(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            CreateHttp2Request(HTTPMethods.Put);

        public override UnityWebRequest CreateUnityWebRequest() =>
            CreateUnityWebRequest("PUT");

        public override (HttpRequestMessage, ulong uploadPayloadSize) CreateYetAnotherHttpRequest() =>
            CreateYetAnotherHttpRequest(HttpMethod.Put);
    }
}
