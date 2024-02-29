using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;

namespace DCL.WebRequests
{
    public class LogWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly Action<string> log;

        public LogWebRequestController(IWebRequestController origin) : this(
            origin,
            value => ReportHub.Log(ReportCategory.GENERIC_WEB_REQUEST, value)
        ) { }

        public LogWebRequestController(IWebRequestController origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask<TWebRequest> SendAsync<TWebRequest, TWebRequestArgs>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope)
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
        {
            try
            {
                log($"WebRequestController send start: {envelope}");
                var result = await origin.SendAsync(envelope);
                log($"WebRequestController send finish: {envelope}");
                return result;
            }
            catch (Exception e)
            {
                var exception = new Exception($"Error during request: {envelope}", e);
                log($"WebRequestController send error: {exception}");
                throw exception;
            }
        }
    }
}
