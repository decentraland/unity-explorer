using Best.HTTP;
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
    }
}
