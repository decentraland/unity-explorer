using Cysharp.Threading.Tasks;
using DCL.WebRequests.RequestsHub;
using System.Text.RegularExpressions;

namespace DCL.WebRequests.Dumper
{
    public class WebRequestDumpRecorder : IWebRequestController
    {
        private readonly IWebRequestController origin;

        IRequestHub IWebRequestController.requestHub => origin.requestHub;

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
            if (instance.Enabled && (string.IsNullOrEmpty(instance.Filter) || Regex.IsMatch(envelope.CommonArguments.URL, instance.Filter))
                                 && envelope.signInfo == null)
                instance.Add(new WebRequestDump.Envelope(typeof(TWebRequest), envelope.CommonArguments, typeof(TWebRequestArgs), envelope.args, envelope.headersInfo));

            return origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
        }
    }
}
