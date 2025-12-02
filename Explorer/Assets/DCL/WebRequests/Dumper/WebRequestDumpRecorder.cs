using Cysharp.Threading.Tasks;
using DCL.WebRequests.RequestsHub;
using System;
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

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            WebRequestsDumper instance = WebRequestsDumper.Instance;

            WebRequestDump.Envelope? dumpEnvelope = null;

            try
            {
                // Signed requests are not supported
                if (instance.IsMatch(envelope.signInfo != null, envelope.CommonArguments.URL))
                    instance.Add(dumpEnvelope = new WebRequestDump.Envelope(typeof(TWebRequest), envelope.CommonArguments, typeof(TWebRequestArgs), envelope.args, envelope.headersInfo, DateTime.Now));

                TResult? result = await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);

                dumpEnvelope?.Conclude(WebRequestDump.Envelope.StatusKind.SUCCESS, DateTime.Now);
                return result;
            }
            catch (Exception)
            {
                dumpEnvelope?.Conclude(WebRequestDump.Envelope.StatusKind.FAILURE, DateTime.Now);
                throw;
            }
        }
    }
}
