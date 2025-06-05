using Best.HTTP;
using System.Net.Http;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericDeleteRequest : GenericUploadRequestBase
    {
        internal GenericDeleteRequest(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            CreateHttp2Request(HTTPMethods.Delete);

        public override UnityWebRequest CreateUnityWebRequest() =>
            CreateUnityWebRequest("DELETE");

        public override HttpRequestMessage CreateYetAnotherHttpRequest() =>
            CreateYetAnotherHttpRequest(HttpMethod.Delete);
    }
}
