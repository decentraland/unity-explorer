using Best.HTTP;
using System.Net.Http;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericGetRequest : TypedWebRequestBase<GenericGetArguments>
    {
        internal GenericGetRequest(RequestEnvelope envelope, GenericGetArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            new (Envelope.CommonArguments.URL, HTTPMethods.Get);

        public override UnityWebRequest CreateUnityWebRequest() =>
            UnityWebRequest.Get(Envelope.CommonArguments.URL);

        public override HttpRequestMessage CreateYetAnotherHttpRequest() =>
            new (HttpMethod.Get, Envelope.CommonArguments.URL);
    }
}
