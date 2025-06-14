using Best.HTTP;
using System.Net.Http;
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

        public override (HttpRequestMessage, ulong uploadPayloadSize) CreateYetAnotherHttpRequest() =>
            new (new HttpRequestMessage(HttpMethod.Head, Envelope.CommonArguments.URL), 0UL);
    }
}
