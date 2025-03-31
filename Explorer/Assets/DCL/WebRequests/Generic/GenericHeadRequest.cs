using Best.HTTP;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericHeadRequest : TypedWebRequestBase<GenericHeadArguments>
    {
        internal GenericHeadRequest(RequestEnvelope envelope, GenericHeadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            new (Envelope.CommonArguments.URL, HTTPMethods.Head);

        public override UnityWebRequest CreateUnityWebRequest() =>
            UnityWebRequest.Head(Envelope.CommonArguments.URL);
    }
}
