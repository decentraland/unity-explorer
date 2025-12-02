using Cysharp.Threading.Tasks;
using DCL.WebRequests.RequestsHub;
using System.Text.RegularExpressions;

namespace DCL.WebRequests.Dumper
{
    public class WebRequestDumpRecorder : IWebRequestController
    {
        private readonly IWebRequestController origin;

        IRequestHub IWebRequestController.RequestHub => origin.RequestHub;

        public WebRequestDumpRecorder(IWebRequestController origin)
        {
            this.origin = origin;
        }

        public UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            WebRequestsDumper instance = WebRequestsDumper.Instance;

            // Signed requests are not supported
            if (instance.IsMatch(envelope.signInfo != null, envelope.CommonArguments.URL))
                instance.Add(new WebRequestDump.Envelope(typeof(TWebRequest), envelope.CommonArguments, typeof(TWebRequestArgs), envelope.args, envelope.headersInfo));

            return origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
        }
    }
}
