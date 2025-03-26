using Best.HTTP;
using DCL.Diagnostics;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public abstract class TypedWebRequestBase<TArgs> : ITypedWebRequest<TArgs> where TArgs: struct
    {
        protected internal TypedWebRequestBase(RequestEnvelope envelope, TArgs args, IWebRequestController controller)
        {
            Envelope = envelope;
            Controller = controller;
            Args = args;
        }

        public IWebRequestController Controller { get; set; }
        public RequestEnvelope Envelope { get; }
        public TArgs Args { get; }

        public virtual UnityWebRequest CreateUnityWebRequest() =>
            throw new NotSupportedException($"{nameof(CreateUnityWebRequest)} is not supported by {GetType().Name}");

        public virtual HTTPRequest CreateHttp2Request() =>
            throw new NotSupportedException($"{nameof(CreateHttp2Request)} is not supported by {GetType().Name}");

        public void Dispose()
        {
            OnDispose();

            Envelope.Dispose();
        }

        protected virtual void OnDispose() { }

        public override string ToString() =>
            $"{GetType().Name}\nArgs: {Args.ToString()}\n{Envelope.CommonArguments.URL}";

        ~TypedWebRequestBase()
        {
            ReportHub.LogError(new ReportData(ReportCategory.GENERIC_WEB_REQUEST, ReportHint.SessionStatic), $"${this} was not disposed properly. It may lead to leaks and crashes");

            Dispose();
        }
    }
}
