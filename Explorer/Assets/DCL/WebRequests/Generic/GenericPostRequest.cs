using Best.HTTP;
using Best.HTTP.Request.Upload.Forms;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GenericPostRequest : GenericUploadRequestBase
    {
        public GenericPostRequest(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
        }

        public override HTTPRequest CreateHttp2Request() =>
            CreateHttp2Request(HTTPMethods.Post);

        public override UnityWebRequest CreateUnityWebRequest() =>
            CreateUnityWebRequest("POST");

        public override HttpRequestMessage CreateYetAnotherHttpRequest() =>
            CreateYetAnotherHttpRequest(HttpMethod.Post);
    }
}
