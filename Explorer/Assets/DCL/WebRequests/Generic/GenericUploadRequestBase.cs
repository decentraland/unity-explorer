using Best.HTTP;
using Best.HTTP.Request.Upload.Forms;
using System;
using System.IO;
using System.Text;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public abstract class GenericUploadRequestBase : TypedWebRequestBase<GenericUploadArguments>
    {
        protected internal GenericUploadRequestBase(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller)
            : base(envelope, args, controller) { }

        protected HTTPRequest CreateHttp2Request(HTTPMethods method)
        {
            var request = new HTTPRequest(Envelope.CommonArguments.URL, method);

            if (Args.MultipartFormSections != null)
            {
                var stream = new MultipartFormDataStream();

                foreach (IMultipartFormSection? section in Args.MultipartFormSections)

                    // There is no non-allocation API
                    stream.AddStreamField(section.sectionName, new MemoryStream(section.sectionData), section.fileName, section.contentType);

                request.UploadSettings.UploadStream = stream;
            }
            else if (Args.WWWForm != null)
                request.UploadSettings.UploadStream = new MemoryStream(Args.WWWForm.data);
            else
            {
                request.SetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER, Args.ContentType);
                request.UploadSettings.UploadStream = new MemoryStream(Args.PostData == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(Args.PostData));
            }

            return request;
        }

        protected UnityWebRequest CreateUnityWebRequest(string method)
        {
            UnityWebRequest wr;

            if (Args.MultipartFormSections != null)
                wr = UnityWebRequest.Post(Envelope.CommonArguments.URL, Args.MultipartFormSections);

            else if (Args.WWWForm != null)
                wr = UnityWebRequest.Post(Envelope.CommonArguments.URL, Args.WWWForm);

            else wr = UnityWebRequest.Post(Envelope.CommonArguments.URL, Args.PostData, Args.ContentType);

            wr.method = method;
            return wr;
        }
    }
}
