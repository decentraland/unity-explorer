using Best.HTTP;

namespace DCL.WebRequests
{
    public class PartialDownloadRequest : TypedWebRequestBase<PartialDownloadArguments>
    {
        internal PartialDownloadRequest(RequestEnvelope envelope, PartialDownloadArguments args, IWebRequestController controller) : base(envelope, args, controller) { }

        public override bool Http2Supported => true;
        public override bool StreamingSupported => true;

        public override HTTPRequest CreateHttp2Request() =>
            new (Envelope.CommonArguments.URL, HTTPMethods.Get);
    }
}
