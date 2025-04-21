using Best.HTTP;
using DCL.Diagnostics;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public abstract class TypedWebRequestBase<TArgs> : ITypedWebRequest<TArgs> where TArgs: struct
    {
        internal bool isDisposed;

        protected RequestEnvelope envelope;

        protected internal TypedWebRequestBase(RequestEnvelope envelope, TArgs args, IWebRequestController controller)
        {
            this.envelope = envelope;
            Controller = controller;
            Args = args;
        }

        public IWebRequestController Controller { get; }
        public ref readonly RequestEnvelope Envelope => ref envelope;
        public TArgs Args { get; }

        public virtual bool Http2Supported => true;
        public virtual long DownloadBufferMaxSize => 30 * 1024 * 1024; // 30MB
        public virtual bool StreamingSupported => false;

        public virtual UnityWebRequest CreateUnityWebRequest() =>
            throw new NotSupportedException($"{nameof(CreateUnityWebRequest)} is not supported by {GetType().Name}");

        public virtual HTTPRequest CreateHttp2Request() =>
            throw new NotSupportedException($"{nameof(CreateHttp2Request)} is not supported by {GetType().Name}");

        protected CommonArguments commonArguments => Envelope.CommonArguments;

        public void Dispose()
        {
            if (isDisposed) return;

            OnDispose();

            Envelope.Dispose();
            isDisposed = true;
        }

        protected virtual void OnDispose() { }

        public override string ToString() =>
            $"{GetType().Name}\nArgs: {Args.ToString()}\n{Envelope.CommonArguments.URL}";

        ~TypedWebRequestBase()
        {
            if (isDisposed) return;

            ReportHub.LogError(new ReportData(ReportCategory.GENERIC_WEB_REQUEST), $"The envelope was not disposed properly. It may lead to leaks and crashes\n{this}");
            Dispose();
        }
    }
}
