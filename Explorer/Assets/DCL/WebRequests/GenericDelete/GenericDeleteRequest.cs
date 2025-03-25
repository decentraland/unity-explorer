using Best.HTTP;
using UnityEngine.Networking;

namespace DCL.WebRequests.GenericDelete
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
    }
}
