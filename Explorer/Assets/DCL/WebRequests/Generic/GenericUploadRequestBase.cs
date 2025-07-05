using Best.HTTP;
using Best.HTTP.Request.Upload.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public abstract class GenericUploadRequestBase : TypedWebRequestBase<GenericUploadArguments>
    {
        protected internal GenericUploadRequestBase(RequestEnvelope envelope, GenericUploadArguments args, IWebRequestController controller)
            : base(envelope, args, controller) { }

        protected (HttpRequestMessage, ulong uploadPayloadSize) CreateYetAnotherHttpRequest(HttpMethod method)
        {
            var request = new HttpRequestMessage(method, Envelope.CommonArguments.URL);

            var uploadSize = 0UL;

            if (Args.MultipartFormSections != null)
            {
                var stream = new MultipartFormDataContent();

                foreach (IMultipartFormSection? section in Args.MultipartFormSections)
                {
                    byte[]? sectionData = section.sectionData;
                    uploadSize += (ulong)sectionData.Length;
                    var content = new ByteArrayContent(sectionData);
                    content.Headers.TryAddWithoutValidation(WebRequestHeaders.CONTENT_TYPE_HEADER, section.contentType);

                    // There is validation in the constructor that may throw an exception
                    if (string.IsNullOrWhiteSpace(section.fileName))
                        stream.Add(content, section.sectionName);
                    else
                        stream.Add(content, section.sectionName, section.fileName);
                }

                request.Content = stream;
            }
            else if (Args.WWWForm != null)
            {
                byte[]? data = Args.WWWForm.data;
                var stream = new ByteArrayContent(data);
                uploadSize += (ulong)data.Length;

                foreach (KeyValuePair<string, string> formHeader in Args.WWWForm.headers)
                    stream.Headers.TryAddWithoutValidation(formHeader.Key, formHeader.Value);

                request.Content = stream;
            }
            else
            {
                byte[] data = Args.PostData == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(Args.PostData);
                uploadSize += (ulong)data.Length;
                var content = new ByteArrayContent(data);
                content.Headers.TryAddWithoutValidation(WebRequestHeaders.CONTENT_TYPE_HEADER, Args.ContentType);
                request.Content = content;
            }

            return (request, uploadSize);
        }

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
            {
                request.UploadSettings.UploadStream = new MemoryStream(Args.WWWForm.data);

                foreach (KeyValuePair<string, string> formHeader in Args.WWWForm.headers)
                    request.SetHeader(formHeader.Key, formHeader.Value);
            }
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
