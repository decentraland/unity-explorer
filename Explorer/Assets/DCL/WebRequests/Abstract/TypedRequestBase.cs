using System;

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

        public void Dispose()
        {
            OnDispose();

            Envelope.Dispose();
        }

        protected virtual void OnDispose() { }
    }
}
